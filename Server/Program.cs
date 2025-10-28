using Microsoft.EntityFrameworkCore;
using Services;
using Data;

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

// --- ATUALIZAÇÃO: Aplicar migrações do AppDbContext na inicialização ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // Aplica migrações do banco de dados principal (PostgreSQL)
    var mainDbContext = services.GetRequiredService<AppDbContext>();
    var canConnect = mainDbContext.Database.CanConnect();
    app.Logger.LogInformation($"Can connect to PostgreSQL database: {canConnect}");

    if (canConnect)
    {
        // Ensure users and pix_transactions tables exist
        mainDbContext.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS public.users (
                id BIGINT NOT NULL,
                email TEXT NOT NULL,
                name TEXT NOT NULL,
                password_hash TEXT NOT NULL,
                is_influencer BOOLEAN NOT NULL DEFAULT FALSE,
                status TEXT NOT NULL DEFAULT 'active',
                created_at TIMESTAMP WITH TIME ZONE NOT NULL,
                updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
                CONSTRAINT pk_users PRIMARY KEY (id),
                CONSTRAINT ix_users_email UNIQUE (email)
            );

            CREATE TABLE IF NOT EXISTS public.pix_transactions (
                id BIGINT NOT NULL,
                user_id BIGINT NOT NULL,
                type TEXT NOT NULL DEFAULT 'PIX_IN',
                id_transaction TEXT NOT NULL,
                amount NUMERIC(18,2) NOT NULL,
                status TEXT NOT NULL DEFAULT 'pending',
                pix_key TEXT NOT NULL,
                pix_key_type TEXT NOT NULL,
                qr_code TEXT NOT NULL,
                qr_code_image_url TEXT NOT NULL,
                created_at TIMESTAMP WITH TIME ZONE NOT NULL,
                paid_at TIMESTAMP WITH TIME ZONE,
                CONSTRAINT pk_pix_transactions PRIMARY KEY (id),
                CONSTRAINT fk_pix_transactions_user_id FOREIGN KEY (user_id) REFERENCES public.users(id),
                CONSTRAINT chk_pix_transaction_status CHECK (status IN ('pending', 'Complete', 'Canceled')),
                CONSTRAINT chk_pix_transaction_type CHECK (type IN ('PIX_IN', 'PIX_OUT'))
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_pix_transactions_id_transaction ON public.pix_transactions (id_transaction);
        ");
        try
        {
            mainDbContext.Database.Migrate();
            app.Logger.LogInformation("PostgreSQL migrations applied successfully.");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Error applying PostgreSQL migrations.");
        }
    }
    else
    {
        app.Logger.LogError("Cannot connect to PostgreSQL database.");
    }

    // Cria o banco de dados de sessão (SQLite) se não existir
    var sessionDbContext = services.GetRequiredService<SessionDbContext>();
    sessionDbContext.Database.EnsureCreated();
}
// --------------------------------------------------------------------

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