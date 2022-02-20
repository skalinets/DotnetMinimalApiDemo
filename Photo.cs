namespace MinimalApi;

public class Photo
{
    public string? Description { get; set; }
}

public class PhotoWithId : Photo
{
    public static readonly PhotoWithId Default = new() { Description = "default photo", Id = -1 };
    public long? Id { get; set; }
}

