using Microsoft.AspNetCore.Mvc;
using Server.Services;

// Create Builder
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<PersonService>();
builder.Services.AddSingleton<PersonCheckServer>();

// Set-up application
var app = builder.Build();

app.MapGet("/api/check/", (PersonService service, PersonCheckServer server, [FromBody] PersonModel person) => service.IsOver18(person));

// Run server
app.Run();
