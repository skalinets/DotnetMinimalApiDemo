var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<ImageFormatValidator>();

var app = builder.Build();

var host = app.Configuration["REDIS_HOST"] ?? "localhost:6379";
var redis = ConnectionMultiplexer.Connect(host).GetDatabase(0);

app.MapPost("/photos", AddPhotoInfo);

app.MapGet("/photos/{id:int?}", GetPhotos)
    .WithName("photos");

app.MapPost("/upload/{id:int}", UploadPhoto)
    .WithName("upload");

app.MapGet("photos/download/{id:int}", DownloadPhoto);

app.MapGet("/metrics", (HttpResponse response)
        => Metrics.DefaultRegistry.CollectAndExportAsTextAsync(response.Body));

app.Run();

async Task<IResult> GetPhotos(int? id)
{
    if (!id.HasValue)
        return Results.Ok(await GetAllPhotos());

    var r = await GetPhoto(id.Value);
    return r == PhotoWithId.Default ? Results.NotFound() : Results.Ok(r);
}

async Task<PhotoWithId[]> GetAllPhotos()
{
    var count = (int)await redis.StringGetAsync("counter");

    return (await Task.WhenAll(Enumerable.Range(1, count + 1)
        .Take(10)
        .Select(GetPhoto)))
        .Where(p => p != PhotoWithId.Default)
        .ToArray();
}

async Task<PhotoWithId> GetPhoto(int id)
{
    var key = $"photo:{id}";
    var r = await redis.ListRangeAsync(key);
    return r.Any() ?
        new PhotoWithId { Description = r[0], Id = id }
        : PhotoWithId.Default;
}

async Task<object> AddPhotoInfo(Photo photo, LinkGenerator generator)
{
    var id = await redis.StringIncrementAsync("counter");
    _ = await redis.KeyDeleteAsync($"photo:{id}");
    _ = await redis.ListLeftPushAsync($"photo:{id}", photo.Description);
    return new
    {
        Link = generator.GetPathByName("photos", new { id }),
        UploadLink = generator.GetPathByName("upload", new { id })
    };
};

async Task<IResult> UploadPhoto(int id, HttpRequest request, ImageFormatValidator validator)
{
    if (!request.HasFormContentType)
        return Results.BadRequest();

    var form = await request.ReadFormAsync();
    var formFile = form.Files["file"];
    if (formFile is null || formFile.Length == 0)
        return Results.BadRequest("'file' content expected");

    if (!validator.IsValid(formFile.FileName))
        return Results.BadRequest("File should be .jpeg or .jgp");

    await using var stream = formFile.OpenReadStream();

    using var fileStream = File.OpenWrite($"files/{id}");

    await stream.CopyToAsync(fileStream);

    return Results.Ok(new { formFile.FileName, id });
}

IResult DownloadPhoto(int id)
{
    var stream = File.OpenRead($"files/{id}");
    return Results.Stream(stream, "image/jpeg");
}

