using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using ErrorAnalysisBackend.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ErrorAnalysisBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;

// --- Configuration ---
var builder = WebApplication.CreateBuilder(args);

// ✅ Load environment variables or appsettings.json (Optional)
var firebaseProjectId = builder.Configuration["Firebase:ProjectId"] ?? "studio-5012646871-facfb";
var serviceAccountPath = builder.Configuration["Firebase:ServiceAccountPath"] ?? "Secrets/serviceAccountKey.json";

// --- Register Basic Services ---
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Firebase Admin & Firestore Setup ---
var credential = GoogleCredential.FromFile(serviceAccountPath);

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
    var firebaseApp = provider.GetRequiredService<FirebaseApp>();
    return new FirestoreDbBuilder
    {
        ProjectId = firebaseProjectId,
        Credential = credential
    }.Build();
});

builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddSingleton<UserAnalysisService>();

// --- 🌐 CORS Setup (Frontend Integration) ---
var allowedOrigins = new[]
{
    "https://d1vimhi8al3qoq.cloudfront.net",        // CloudFront frontend
    "https://stackseek.io",                         // Production domain
    "http://localhost:5173",                        // Local frontend dev
    "http://localhost:5174",                        // Alternate local port
    "http://localhost:3000",                        // Common React dev port
};

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// --- 🔐 Authentication (Firebase JWT) ---
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

// --- Build App ---
var app = builder.Build();

// --- 🌱 Basic Routes ---
app.MapGet("/", () => Results.Ok(new { status = "ok", ts = DateTime.UtcNow }));
app.MapGet("/health", () => Results.Ok("healthy"));

// --- Swagger + Dev Error Handling ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    // app.UseHsts(); // Optional
}

// --- Middleware Order ---
app.UseRouting();
app.UseCors("FrontendPolicy");

if (app.Environment.IsDevelopment())
{
    app.UseMiddleware<DevelopmentUserMiddleware>();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// --- ✅ Run App ---
app.Run();
