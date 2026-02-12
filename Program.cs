using Slavyan.Services;
using Vertex.Services;
using Vertex.Settings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSingleton<PostFileStore>();
builder.Services.Configure<MetaSettings>(builder.Configuration.GetSection("MetaSettings"));
builder.Services.AddHttpClient<InstagramService>();
builder.Services.AddHttpClient<FacebookService>();
builder.Services.AddTransient<SocialPublisher>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("LiveServer", p =>
        p.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod());
});

builder.Services.Configure<AzureStorageOptions>(builder.Configuration.GetSection("AzureStorage"));
builder.Services.AddSingleton<AzureBlobStorageService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("LiveServer");
app.UseAuthorization();
app.UseStaticFiles();
app.MapControllers();

app.Run();
