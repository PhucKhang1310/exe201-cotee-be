namespace CooTee.Configuration;





public class MongoDbSettings
{
    
    
    
    
    public string ConnectionString { get; set; } = string.Empty;

    
    
    
    
    public string DatabaseName { get; set; } = string.Empty;

    
    
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("MongoDbSettings.ConnectionString không được để trống");

        if (string.IsNullOrWhiteSpace(DatabaseName))
            throw new InvalidOperationException("MongoDbSettings.DatabaseName không được để trống");
    }
}
