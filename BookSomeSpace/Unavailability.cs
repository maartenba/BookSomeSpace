using System;

namespace BookSomeSpace;

public class Unavailability
{
    public Unavailability(string id, DateTime start, DateTime end)
    {
        Id = id;
        Start = start;
        End = end;
    }
        
    public string Id { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}