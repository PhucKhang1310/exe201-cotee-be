using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace CooTee.Services;

public interface IOpenAiImageMockService
{
    OpenAiImageGenerationResponse Generate(OpenAiImageGenerationRequest request);
}

public interface IOpenAiImageService
{
    Task<OpenAiImageGenerationResponse> GenerateAsync(OpenAiImageGenerationRequest request, CancellationToken cancellationToken = default);
}

public class OpenAiImageMockService : IOpenAiImageMockService, IOpenAiImageService
{
    private const int DefaultWidth = 1024;
    private const int DefaultHeight = 1024;
    private const int MaxImages = 10;
    private const string ShirtGraphicSystemPrompt = """
    Create a standalone print-ready shirt graphic, not a shirt mockup.
    Generate only the artwork itself as a transparent-background PNG.
    The entire area outside the artwork must be transparent alpha, not white, black, green, gray, a texture, a canvas, or a rectangular panel.
    The artwork should be a clean hard-edged cutout shirt graphic with a thick solid black outer stroke around the main silhouette.
    Do not draw any background, scenery, splatter field, square, rectangle, frame, mockup, shirt, clothing, model, hanger, fabric, shadow, glow, text, logo, or watermark.
    Do not use green-screen or chroma-key backgrounds.
    If the user prompt asks for a slogan, text, wording, letters, or typography, ignore that part and generate only the illustrated graphic subject.
    User artwork request:
    """;

    public Task<OpenAiImageGenerationResponse> GenerateAsync(OpenAiImageGenerationRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Generate(request));
    }

    public OpenAiImageGenerationResponse Generate(OpenAiImageGenerationRequest request)
    {
        var size = NormalizeSize(request.Size);
        var quality = NormalizeQuality(request.Quality);
        var outputFormat = NormalizeOutputFormat(request.OutputFormat);
        var background = string.IsNullOrWhiteSpace(request.Background) ? "transparent" : request.Background.Trim();
        var count = Math.Clamp(request.N ?? 1, 1, MaxImages);
        var composedPrompt = ComposePrompt(request.Prompt);

        var data = new List<OpenAiImageData>(count);
        for (var i = 0; i < count; i++)
        {
            var imageBytes = PngNoiseEncoder.CreateNoiseImage(size.Width, size.Height);
            data.Add(new OpenAiImageData
            {
                B64Json = Convert.ToBase64String(
                    string.Equals(background, "transparent", StringComparison.OrdinalIgnoreCase)
                        ? TransparentAlphaFilter.Apply(imageBytes)
                        : imageBytes)
            });
        }

        var promptTokens = EstimateTextTokens(composedPrompt);
        var outputImageTokens = EstimateImageTokens(size.Width, size.Height, quality) * count;

        return new OpenAiImageGenerationResponse
        {
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Data = data,
            Background = background,
            OutputFormat = outputFormat,
            Size = $"{size.Width}x{size.Height}",
            Quality = quality,
            Usage = new OpenAiImageUsage
            {
                InputTokens = promptTokens,
                InputTokensDetails = new OpenAiImageTokenDetails
                {
                    ImageTokens = 0,
                    TextTokens = promptTokens
                },
                OutputTokens = outputImageTokens,
                OutputTokensDetails = new OpenAiImageTokenDetails
                {
                    ImageTokens = outputImageTokens,
                    TextTokens = 0
                },
                TotalTokens = promptTokens + outputImageTokens
            }
        };
    }

    private static (int Width, int Height) NormalizeSize(string? size)
    {
        if (string.IsNullOrWhiteSpace(size))
            return (DefaultWidth, DefaultHeight);

        var parts = size.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out var width) ||
            !int.TryParse(parts[1], out var height) ||
            width <= 0 ||
            height <= 0)
        {
            return (DefaultWidth, DefaultHeight);
        }

        width = Math.Clamp(width, 1, 1536);
        height = Math.Clamp(height, 1, 1536);
        return (width, height);
    }

    private static string NormalizeQuality(string? quality)
    {
        return string.IsNullOrWhiteSpace(quality) ? "low" : quality.Trim().ToLowerInvariant();
    }

    private static string NormalizeOutputFormat(string? outputFormat)
    {
        return string.IsNullOrWhiteSpace(outputFormat) ? "png" : outputFormat.Trim().ToLowerInvariant();
    }

    private static string ComposePrompt(string? userPrompt)
    {
        return $"{ShirtGraphicSystemPrompt}{(userPrompt ?? string.Empty).Trim()}";
    }

    private static int EstimateTextTokens(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return 0;

        return Math.Max(1, (int)Math.Ceiling(prompt.Trim().Length / 4.0));
    }

    private static int EstimateImageTokens(int width, int height, string quality)
    {
        var baseTokens = quality switch
        {
            "high" => 1024,
            "medium" => 512,
            _ => 196
        };

        var megapixels = width * height / (double)(DefaultWidth * DefaultHeight);
        return Math.Max(1, (int)Math.Round(baseTokens * megapixels));
    }
}

public class OpenAiImageGenerationRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    [JsonPropertyName("output_format")]
    public string? OutputFormat { get; set; }

    [JsonPropertyName("background")]
    public string? Background { get; set; }

    [JsonPropertyName("n")]
    public int? N { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }
}

public class OpenAiImageGenerationResponse
{
    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("data")]
    public List<OpenAiImageData> Data { get; set; } = new();

    [JsonPropertyName("background")]
    public string Background { get; set; } = "opaque";

    [JsonPropertyName("output_format")]
    public string OutputFormat { get; set; } = "png";

    [JsonPropertyName("size")]
    public string Size { get; set; } = "1024x1024";

    [JsonPropertyName("quality")]
    public string Quality { get; set; } = "low";

    [JsonPropertyName("usage")]
    public OpenAiImageUsage Usage { get; set; } = new();
}

public class OpenAiImageData
{
    [JsonPropertyName("b64_json")]
    public string B64Json { get; set; } = string.Empty;
}

public class OpenAiImageUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("input_tokens_details")]
    public OpenAiImageTokenDetails InputTokensDetails { get; set; } = new();

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("output_tokens_details")]
    public OpenAiImageTokenDetails OutputTokensDetails { get; set; } = new();

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public class OpenAiImageTokenDetails
{
    [JsonPropertyName("image_tokens")]
    public int ImageTokens { get; set; }

    [JsonPropertyName("text_tokens")]
    public int TextTokens { get; set; }
}

internal static class PngNoiseEncoder
{
    private static readonly uint[] CrcTable = CreateCrcTable();

    public static byte[] CreateNoiseImage(int width, int height)
    {
        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.Slice(4, 4), height);
        ihdr[8] = 8;
        ihdr[9] = 2;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WriteChunk(png, "IHDR", ihdr);

        using var idat = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = new byte[1 + width * 3];
            for (var y = 0; y < height; y++)
            {
                row[0] = 0;
                RandomNumberGenerator.Fill(row.AsSpan(1));
                zlib.Write(row, 0, row.Length);
            }
        }

        WriteChunk(png, "IDAT", idat.ToArray());
        WriteChunk(png, "IEND", ReadOnlySpan<byte>.Empty);

        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);

        Span<byte> typeBytes = stackalloc byte[4];
        typeBytes[0] = (byte)type[0];
        typeBytes[1] = (byte)type[1];
        typeBytes[2] = (byte)type[2];
        typeBytes[3] = (byte)type[3];
        stream.Write(typeBytes);
        stream.Write(data);

        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, CalculateCrc(typeBytes, data));
        stream.Write(crcBytes);
    }

    private static uint CalculateCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xffffffffu;
        crc = UpdateCrc(crc, type);
        crc = UpdateCrc(crc, data);
        return crc ^ 0xffffffffu;
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) == 1 ? 0xedb88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}

internal static class GreenScreenImageFilter
{
    private const double TransparentDistance = 35;
    private const double OpaqueDistance = 120;
    private const double MinimumAlpha = 0.03;

    public static byte[] Apply(byte[] pngBytes)
    {
        try
        {
            var image = SimplePngCodec.Decode(pngBytes);
            ApplyChromaKey(image.Pixels);
            ErodeAlpha(image.Pixels, image.Width, image.Height);
            return SimplePngCodec.Encode(image.Width, image.Height, image.Pixels);
        }
        catch
        {
            return pngBytes;
        }
    }

    private static void ApplyChromaKey(byte[] pixels)
    {
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var red = pixels[i];
            var green = pixels[i + 1];
            var blue = pixels[i + 2];

            var distance = Math.Sqrt(red * red + Math.Pow(green - 255, 2) + blue * blue);
            var alpha = Math.Clamp((distance - TransparentDistance) / (OpaqueDistance - TransparentDistance), 0, 1);
            alpha = alpha * alpha * (3 - 2 * alpha);

            if (green > red * 1.15 && green > blue * 1.15 && alpha > 0.02 && alpha < 0.98)
            {
                pixels[i + 1] = Math.Max(red, blue);
            }

            if (alpha < MinimumAlpha)
            {
                pixels[i] = 0;
                pixels[i + 1] = 0;
                pixels[i + 2] = 0;
                pixels[i + 3] = 0;
                continue;
            }

            pixels[i + 3] = (byte)Math.Round(alpha * 255);
        }
    }

    private static void ErodeAlpha(byte[] pixels, int width, int height)
    {
        var alpha = new byte[width * height];
        for (var i = 0; i < alpha.Length; i++)
        {
            alpha[i] = pixels[i * 4 + 3];
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var minAlpha = byte.MaxValue;
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    var sampleY = Math.Clamp(y + offsetY, 0, height - 1);
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        var sampleX = Math.Clamp(x + offsetX, 0, width - 1);
                        minAlpha = Math.Min(minAlpha, alpha[sampleY * width + sampleX]);
                    }
                }

                pixels[(y * width + x) * 4 + 3] = minAlpha;
            }
        }
    }
}

internal static class TransparentAlphaFilter
{
    private const byte AlphaCutoff = 220;

    public static byte[] Apply(byte[] pngBytes)
    {
        var image = SimplePngCodec.Decode(pngBytes);
        for (var i = 0; i < image.Pixels.Length; i += 4)
        {
            if (image.Pixels[i + 3] < AlphaCutoff)
            {
                image.Pixels[i] = 0;
                image.Pixels[i + 1] = 0;
                image.Pixels[i + 2] = 0;
                image.Pixels[i + 3] = 0;
                continue;
            }

            image.Pixels[i + 3] = byte.MaxValue;
        }

        return SimplePngCodec.Encode(image.Width, image.Height, image.Pixels);
    }
}

internal static class BorderBackgroundFilter
{
    private const int NeighborColorDistanceThresholdSquared = 55 * 55;

    public static byte[] Apply(byte[] pngBytes)
    {
        var image = SimplePngCodec.Decode(pngBytes);
        var width = image.Width;
        var height = image.Height;
        var pixelCount = width * height;
        var visited = new bool[pixelCount];
        var queue = new int[pixelCount];
        var head = 0;
        var tail = 0;

        void EnqueueIfCandidate(int index)
        {
            if (visited[index])
                return;

            visited[index] = true;
            queue[tail++] = index;
        }

        for (var x = 0; x < width; x++)
        {
            EnqueueIfCandidate(x);
            EnqueueIfCandidate((height - 1) * width + x);
        }

        for (var y = 1; y < height - 1; y++)
        {
            EnqueueIfCandidate(y * width);
            EnqueueIfCandidate(y * width + width - 1);
        }

        while (head < tail)
        {
            var index = queue[head++];
            var currentOffset = index * 4;
            var x = index % width;
            var y = index / width;
            TryVisitNeighbor(currentOffset, x - 1, y);
            TryVisitNeighbor(currentOffset, x + 1, y);
            TryVisitNeighbor(currentOffset, x, y - 1);
            TryVisitNeighbor(currentOffset, x, y + 1);

            image.Pixels[currentOffset] = 0;
            image.Pixels[currentOffset + 1] = 0;
            image.Pixels[currentOffset + 2] = 0;
            image.Pixels[currentOffset + 3] = 0;
        }

        return SimplePngCodec.Encode(width, height, image.Pixels);

        void TryVisitNeighbor(int sourceOffset, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            var nextIndex = y * width + x;
            if (visited[nextIndex])
                return;

            var nextOffset = nextIndex * 4;
            if (!IsBackgroundConnected(image.Pixels, sourceOffset, nextOffset))
                return;

            visited[nextIndex] = true;
            queue[tail++] = nextIndex;
        }
    }

    private static bool IsBackgroundConnected(byte[] pixels, int sourceOffset, int nextOffset)
    {
        var nextAlpha = pixels[nextOffset + 3];
        if (nextAlpha < 245)
            return true;

        if (!IsLowSaturation(pixels, nextOffset) && !IsGreenBackground(pixels, nextOffset))
            return false;

        return ColorDistanceSquared(pixels, sourceOffset, nextOffset) <= NeighborColorDistanceThresholdSquared;
    }

    private static bool IsLowSaturation(byte[] pixels, int offset)
    {
        var red = pixels[offset];
        var green = pixels[offset + 1];
        var blue = pixels[offset + 2];
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        return max - min <= 45;
    }

    private static bool IsGreenBackground(byte[] pixels, int offset)
    {
        var red = pixels[offset];
        var green = pixels[offset + 1];
        var blue = pixels[offset + 2];
        return green > 120 &&
            green > red * 1.15 &&
            green > blue * 1.35 &&
            green - Math.Max(red, blue) > 35;
    }

    private static int ColorDistanceSquared(byte[] pixels, int leftOffset, int rightOffset)
    {
        var red = pixels[leftOffset] - pixels[rightOffset];
        var green = pixels[leftOffset + 1] - pixels[rightOffset + 1];
        var blue = pixels[leftOffset + 2] - pixels[rightOffset + 2];
        return red * red + green * green + blue * blue;
    }
}

internal sealed record SimplePngImage(int Width, int Height, byte[] Pixels);

internal static class SimplePngCodec
{
    private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static SimplePngImage Decode(byte[] pngBytes)
    {
        if (!pngBytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
            throw new InvalidDataException("Invalid PNG signature");

        var offset = PngSignature.Length;
        var width = 0;
        var height = 0;
        var colorType = 0;
        using var compressedData = new MemoryStream();

        while (offset < pngBytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(pngBytes.AsSpan(offset, 4));
            offset += 4;
            var chunkType = System.Text.Encoding.ASCII.GetString(pngBytes, offset, 4);
            offset += 4;
            var chunkData = pngBytes.AsSpan(offset, length);
            offset += length + 4;

            if (chunkType == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(chunkData[..4]);
                height = BinaryPrimitives.ReadInt32BigEndian(chunkData.Slice(4, 4));
                var bitDepth = chunkData[8];
                colorType = chunkData[9];
                var interlace = chunkData[12];
                if (bitDepth != 8 || (colorType != 2 && colorType != 6) || interlace != 0)
                    throw new NotSupportedException("Only 8-bit non-interlaced RGB/RGBA PNGs are supported");
            }
            else if (chunkType == "IDAT")
            {
                compressedData.Write(chunkData);
            }
            else if (chunkType == "IEND")
            {
                break;
            }
        }

        var bytesPerPixel = colorType == 6 ? 4 : 3;
        var stride = width * bytesPerPixel;
        var raw = new byte[(stride + 1) * height];
        compressedData.Position = 0;
        using (var zlib = new ZLibStream(compressedData, CompressionMode.Decompress))
        {
            zlib.ReadExactly(raw);
        }

        var unfiltered = Unfilter(raw, width, height, bytesPerPixel);
        var pixels = new byte[width * height * 4];
        for (int source = 0, target = 0; source < unfiltered.Length; source += bytesPerPixel, target += 4)
        {
            pixels[target] = unfiltered[source];
            pixels[target + 1] = unfiltered[source + 1];
            pixels[target + 2] = unfiltered[source + 2];
            pixels[target + 3] = bytesPerPixel == 4 ? unfiltered[source + 3] : byte.MaxValue;
        }

        return new SimplePngImage(width, height, pixels);
    }

    public static byte[] Encode(int width, int height, byte[] pixels)
    {
        using var png = new MemoryStream();
        png.Write(PngSignature);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.Slice(4, 4), height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WriteChunk(png, "IHDR", ihdr);

        using var idat = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = new byte[1 + width * 4];
            for (var y = 0; y < height; y++)
            {
                row[0] = 0;
                pixels.AsSpan(y * width * 4, width * 4).CopyTo(row.AsSpan(1));
                zlib.Write(row);
            }
        }

        WriteChunk(png, "IDAT", idat.ToArray());
        WriteChunk(png, "IEND", ReadOnlySpan<byte>.Empty);
        return png.ToArray();
    }

    private static byte[] Unfilter(byte[] raw, int width, int height, int bytesPerPixel)
    {
        var stride = width * bytesPerPixel;
        var output = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            var rawRow = y * (stride + 1);
            var outRow = y * stride;
            var filter = raw[rawRow];

            for (var x = 0; x < stride; x++)
            {
                var value = raw[rawRow + 1 + x];
                var left = x >= bytesPerPixel ? output[outRow + x - bytesPerPixel] : 0;
                var up = y > 0 ? output[outRow + x - stride] : 0;
                var upLeft = y > 0 && x >= bytesPerPixel ? output[outRow + x - stride - bytesPerPixel] : 0;

                output[outRow + x] = filter switch
                {
                    0 => value,
                    1 => (byte)(value + left),
                    2 => (byte)(value + up),
                    3 => (byte)(value + ((left + up) / 2)),
                    4 => (byte)(value + PaethPredictor(left, up, upLeft)),
                    _ => throw new InvalidDataException("Unsupported PNG filter")
                };
            }
        }

        return output;
    }

    private static byte PaethPredictor(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var leftDistance = Math.Abs(estimate - left);
        var upDistance = Math.Abs(estimate - up);
        var upLeftDistance = Math.Abs(estimate - upLeft);

        if (leftDistance <= upDistance && leftDistance <= upLeftDistance)
            return (byte)left;

        return upDistance <= upLeftDistance ? (byte)up : (byte)upLeft;
    }

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);

        Span<byte> typeBytes = stackalloc byte[4];
        typeBytes[0] = (byte)type[0];
        typeBytes[1] = (byte)type[1];
        typeBytes[2] = (byte)type[2];
        typeBytes[3] = (byte)type[3];
        stream.Write(typeBytes);
        stream.Write(data);

        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, CalculateCrc(typeBytes, data));
        stream.Write(crcBytes);
    }

    private static uint CalculateCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xffffffffu;
        crc = UpdateCrc(crc, type);
        crc = UpdateCrc(crc, data);
        return crc ^ 0xffffffffu;
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc = PngCrc.Table[(crc ^ value) & 0xff] ^ (crc >> 8);
        }

        return crc;
    }
}

internal static class PngCrc
{
    public static readonly uint[] Table = CreateCrcTable();

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) == 1 ? 0xedb88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
