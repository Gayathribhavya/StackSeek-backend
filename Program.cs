using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using ErrorAnalysisBackend.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ErrorAnalysisBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// Firebase Project ID
var firebaseProjectId = "studio-5012646871-facfb";   // keep hard-coded or take from env if you want

// Basic Services
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- Firebase & Firestore Setup ----
var credentialPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");

if (string.IsNullOrEmpty(credentialPath))
{
    Console.WriteLine("GOOGLE_APPLICATION_CREDENTIALS env variable NOT FOUND!");
}

var credential = GoogleCredential.FromFile(credentialPath);

builder.Services.AddSingleton(provider =>
{
    if (FirebaseApp.DefaultInstance == null)
    {
        return FirebaseApp.Create(new AppOptions
        {
            Credential = credential
        });
    }
    return FirebaseApp.DefaultInstance;
});

builder.Services.AddSingleton(provider =>
{
    return new FirestoreDbBuilder
    {
        ProjectId = firebaseProjectId,
        Credential = credential
    }.Build();
});

builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddSingleton<UserAnalysisService>();

// ---- CORS ----
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ---- JWT Firebase ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://securetoken.google.com/{firebaseProjectId}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",
            ValidateAudience = true,
            ValidAudience = firebaseProjectId,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { status = "ok", ts = DateTime.UtcNow }));
app.MapGet("/health", () => Results.Ok("healthy"));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
