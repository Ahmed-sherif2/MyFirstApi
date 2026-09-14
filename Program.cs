using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions((options) =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.KebabCaseUpper;
});
builder.Services.AddControllers();

Log.Logger = new LoggerConfiguration()
.WriteTo.Console()
.WriteTo.File("logs.txt", rollingInterval: RollingInterval.Day)
.CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.UseWhen(context => context.Request.Method != "GET", appbuilder =>
appbuilder.Use(async (context, next) =>
{
    var password = context.Request.Headers["PASSWORD"];
    if (password == "badpassword")
    {
        await next.Invoke();
    }
    else
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Invalid api key");
    }

}));
app.Use(async (context, next) =>
{
    if (context.Request.Query["secure"] != "true")
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Not secure you should be https");
        return;
    }
    await next.Invoke();
});
app.Use(async (context, next) =>
{
    Console.WriteLine("before next");
    await next.Invoke();
    Console.WriteLine("after next");
});


var blogs = new List<Blog>()
{
  new Blog {title = "my first blog", name="first blog"},
  new Blog {title = "my second blog", name = "second blog"}
};
var samplePerson = new Person { userName = "Ahmed", userAge = 12 };

app.MapGet("/", () => "hello 9");
app.MapPut("/put", () => "hello super 9");
app.MapPost("/blog", (Blog blog) =>
{
    var validationError = ValidateBlog(blog);
    if (validationError is not null)
    {
        return Results.BadRequest(validationError);
    }

    blogs.Add(blog);
    return Results.Created($"/blogs{blogs.Count - 1}", blog);
});
app.MapGet("blogs/{id?}", (int? id) =>
{
    if (id == null)
        return Results.Ok(blogs);
    else
        return Results.Ok(blogs[id.Value]);


});

app.MapDelete("/blogs/{id}", (int id) =>
{
    if (id < 0 || id >= blogs.Count)
    {
        return Results.NotFound();
    }

    var blog = blogs[id];
    blogs.RemoveAt(id);
    return Results.Ok(blog);
});

app.MapGet("/manual-json", () =>
{
    var json = JsonSerializer.Serialize(samplePerson);
    return TypedResults.Text(json, "application/json");
});
app.MapGet("/custom-json", () =>
{
    var options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    var json = JsonSerializer.Serialize(samplePerson, options);
    return TypedResults.Text(json, "application/json");
});
app.MapGet("/json", () =>
{
    return TypedResults.Json(samplePerson);
});
app.MapGet("/auto", () =>
{
    return samplePerson;
});
app.MapGet("/xml", () =>
{
    var xmlserializer = new XmlSerializer(typeof(Person));
    var stringwriter = new StringWriter();
    xmlserializer.Serialize(stringwriter, samplePerson);
    var xmloutput = stringwriter.ToString();
    return TypedResults.Text(xmloutput, "application/xml");

});

app.MapPost("/post", async (HttpContext context) =>
{
    var options = new JsonSerializerOptions
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    var sample = await context.Request.ReadFromJsonAsync<Person>(options);
    if (sample is null)
    {
        return Results.BadRequest("Request body is required.");
    }

    var validationError = ValidatePerson(sample);
    if (validationError is not null)
    {
        return Results.BadRequest(validationError);
    }

    return TypedResults.Json(sample);
});
app.MapPost("/xml-post", async (HttpContext context) =>
{
    StreamReader reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    var xml = new XmlSerializer(typeof(Person));
    var stringReader = new StringReader(body);
    var result = xml.Deserialize(stringReader);
    return TypedResults.Ok(result);


});

string? ValidateBlog(Blog blog)
{
    if (string.IsNullOrWhiteSpace(blog.title))
    {
        return "Title is required.";
    }

    if (blog.title.Length < 3)
    {
        return "Title must be at least 3 characters long.";
    }

    if (string.IsNullOrWhiteSpace(blog.name))
    {
        return "Name is required.";
    }

    if (blog.name.Length < 3)
    {
        return "Name must be at least 3 characters long.";
    }

    return null;
}

string? ValidatePerson(Person person)
{
    if (string.IsNullOrWhiteSpace(person.userName))
    {
        return "userName is required.";
    }

    if (person.userName.Length < 2)
    {
        return "userName must be at least 2 characters long.";
    }

    if (person.userAge is < 0 or > 120)
    {
        return "userAge must be between 0 and 120.";
    }

    return null;
}

app.Run();


public class Blog
{
    [Required]
    [MinLength(3)]
    public required string title { get; set; }

    [Required]
    [MinLength(3)]
    public required string name { get; set; }
}

public class Person
{
    [Required]
    [MinLength(2)]
    public required string userName { get; set; }

    [Range(0, 120)]
    public int? userAge { get; set; }
}