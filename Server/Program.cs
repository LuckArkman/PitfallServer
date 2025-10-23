using Microsoft.EntityFrameworkCore;
using Services;
using Data;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// --- Banco de Dados ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- ATUALIZAÇÃO: Adicionar o DbContext para o banco de dados de sessão (SQLite) ---
builder.Services.AddDbContext<SessionDbContext>(options =>
    options.UseSqlite("Data Source=sessions.db")); // Cria um arquivo sessions.db

// --- Serviços da aplicação ---
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<AdminTokenService>();
builder.Services.AddScoped<SessionService>();

// --- Infraestrutura padrão ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("Data") // força o assembly correto
    )
);

var app = builder.Build();

// --- ATUALIZAÇÃO: Garante que o banco de dados SQLite seja criado na inicialização ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<SessionDbContext>();
    // Cria o banco de dados e o schema se eles não existirem.
    context.Database.EnsureCreated();
}

// --- Pipeline HTTP ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();