using MongoDB.Bson.Serialization.Attributes;

namespace DTOs;

public class UserRanking
{
    [BsonId]
    public Guid id { get; set; } = Guid.NewGuid();
    public Guid _userId { get; set; }
    public int _level { get; set; } = 1;
    public long _points { get; set; } = 0;
    public int _registers { get; set; } = 0;
    public long _pointsNextLevel { get; set; } = 10;

    public void OnLevelUp()
    {
        _points++;
        _registers++;
        if (_points >= _pointsNextLevel)
        {
            _level++;
            _pointsNextLevel = (int)( _pointsNextLevel * 1.5f);
            _points = 0;
        }
    }
}