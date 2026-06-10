// Initialize MongoDB database and collections
// This script runs when MongoDB container starts

// Switch to CoTeeDb database
db = db.getSiblingDB('CoTeeDb');

// Create users collection if it doesn't exist
db.createCollection('users', {
  validator: {
    $jsonSchema: {
      bsonType: 'object',
      required: ['email', 'passwordHash', 'fullName'],
      properties: {
        _id: { bsonType: 'objectId' },
        email: { bsonType: 'string', pattern: '^[^@]+@[^@]+\\.[^@]+$' },
        passwordHash: { bsonType: 'string' },
        fullName: { bsonType: 'string' },
        role: { bsonType: 'string', enum: ['Customer', 'User', 'Admin', 'Manager'] },
        isEmailVerified: { bsonType: 'bool' },
        verificationToken: { bsonType: ['string', 'null'] },
        tokenExpiresAt: { bsonType: ['date', 'null'] },
        verificationEmailLastSentAt: { bsonType: ['date', 'null'] },
        passwordResetToken: { bsonType: ['string', 'null'] },
        passwordResetTokenExpiresAt: { bsonType: ['date', 'null'] },
        createdAt: { bsonType: 'date' },
        updatedAt: { bsonType: 'date' },
        isActive: { bsonType: 'bool' }
      }
    }
  }
});

// Create indexes for better query performance

// Unique index on email
db.users.createIndex(
  { email: 1 },
  { unique: true, name: 'email_unique' }
);

// Tokens are validated by the API. A TTL index here would delete the entire user document.
db.users.createIndex(
  { verificationToken: 1 },
  { sparse: true, name: 'verification_token_index' }
);

db.users.createIndex(
  { passwordResetToken: 1 },
  { sparse: true, name: 'password_reset_token_index' }
);

// Index on role for faster filtering
db.users.createIndex(
  { role: 1 },
  { name: 'role_index' }
);

// Index on isActive for filtering active users
db.users.createIndex(
  { isActive: 1 },
  { name: 'is_active_index' }
);

// Compound index for common queries
db.users.createIndex(
  { isActive: 1, role: 1 },
  { name: 'active_role_compound' }
);

// Index on createdAt for sorting
db.users.createIndex(
  { createdAt: -1 },
  { name: 'created_at_desc' }
);

// Text index for search
db.users.createIndex(
  { email: 'text', fullName: 'text' },
  { name: 'text_search' }
);

print('✅ Database CoTeeDb initialized successfully!');
print('✅ Collection "users" created with schema validation');
print('✅ Indexes created for optimal performance');

// Print summary
var collections = db.getCollectionNames();
print('\n📊 Collections:');
collections.forEach(col => {
  var count = db.getCollection(col).countDocuments({});
  print(`  - ${col} (${count} documents)`);
});

var indexes = db.users.getIndexes();
print('\n📑 Indexes on users collection:');
indexes.forEach(index => {
  print(`  - ${index.name}`);
});
