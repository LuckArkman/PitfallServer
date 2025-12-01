using MongoDB.Driver;

namespace Data;

public class MongoDataController
{
    IMongoClient client;
    string databaseName;
    string collectionName;
    IMongoDatabase _database;
    public MongoDataController()
    {
        
    }

    public MongoDataController(string connectionString, string _databaseName, string _collectionName) // Updated constructor
    {
        this.client = new MongoClient(connectionString);
        this.databaseName = _databaseName;
        this.collectionName = _collectionName; // Store collection name
        GetOrCreateDatabase();
    }

    public IMongoDatabase GetDatabase()
    {
        return _database;
    }

    public void GetOrCreateDatabase()
    {
        _database = client.GetDatabase(databaseName);
        var collectionList = _database.ListCollectionNames().ToList();
        // Use the stored collectionName to create the collection
        if (collectionList.Count <= 0) _database.CreateCollection(collectionName); 
    }
}