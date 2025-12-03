using Services;
using Microsoft.AspNetCore.Identity;
using DTOs;
using Interfaces;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Polly;
using Polly.Extensions.Http;
using Repositorios;

var builder = WebApplication.CreateBuilder(args);
// --- 🔹 Obter Connection Strings ---
var sessionConnection = builder.Configuration.GetConnectionString("SessionConnection")
    ?? "Data Source=sessions.db"; // fallback para SQLite

// --- 🔹 Registrar serviços diretos (sem EF) ---
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<WalletWithdrawSnapshot>();
builder.Services.AddScoped<SessionService>(_ => new SessionService(sessionConnection));
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<PixService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<WalletLedgerService>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<AdminTokenService>();
builder.Services.AddSingleton(typeof(IRepositorio<>), typeof(Repositorio<>));
builder.Services.AddScoped<UserRankingService>();


// --- 🔹 HttpClient (para PixService) ---
builder.Services.AddHttpClient<PixService>(c =>
    {
        c.BaseAddress = new Uri(builder.Configuration["agilizepay:BaseUrl"] ?? "https://api.agilizepay.com/pix");
        c.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(8, attempt)), // 8s, 64s, etc.
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Console.WriteLine($"[agilizepay] Tentativa {retryAttempt} falhou. Repetindo em {timespan.TotalSeconds}s...");
            }
        )
    );
// --- 🔹 Configurar CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin => true) // Aceita QUALQUER origem
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// --- 🔹 Infraestrutura padrão ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- 🔹 Construir o app ---
var app = builder.Build();

// --- 🔹 Middlewares ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection(); // força HTTPS automaticamente
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// --- 🔹 Timer para expirar PIX ---
var timer = new PeriodicTimer(TimeSpan.FromMinutes(600));
_ = Task.Run(async () =>
{
    while (await timer.WaitForNextTickAsync())
    {
        using var scope = app.Services.CreateScope();
        var pixService = scope.ServiceProvider.GetRequiredService<PixService>();
        await pixService.CancelExpiredPixTransactionsAsync(600);
    }
});

// --- 🔹 Executar ---
app.Run();
