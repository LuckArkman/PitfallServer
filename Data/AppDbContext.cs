using Npgsql;
using System;
using System.Threading.Tasks;

namespace Data
{
    public class AppDbContext : IDisposable
    {
        private readonly string _connectionString;
        private NpgsqlConnection _connection;

        public AppDbContext(string connectionString)
        {
            _connectionString = connectionString;
            _connection = new NpgsqlConnection(_connectionString);
            _connection.Open();

            // Cria todas as tabelas ao iniciar o DbContext
            CreateTables().Wait();
        }
        public async Task CreateTables()
        {
            string sql = @"
                CREATE TABLE IF NOT EXISTS Afiliados (
                    Id SERIAL PRIMARY KEY,
                    Nome VARCHAR(150) NOT NULL,
                    Email VARCHAR(150) UNIQUE NOT NULL,
                    DataCadastro TIMESTAMP NOT NULL DEFAULT NOW()
                );

                CREATE TABLE IF NOT EXISTS Codes (
                    Id SERIAL PRIMARY KEY,
                    AfiliadoId INTEGER NOT NULL,
                    Af_key VARCHAR(150) NOT NULL UNIQUE,
                    Af_level INTEGER NOT NULL DEFAULT 1,
                    Nome VARCHAR(150) NOT NULL,
                    Email VARCHAR(150) UNIQUE NOT NULL,
                    DataRegistro TIMESTAMP NOT NULL DEFAULT NOW(),
                    FOREIGN KEY (AfiliadoId) REFERENCES public.users(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS Referidos (
                    Id SERIAL PRIMARY KEY,
                    AfiliadoId INTEGER NOT NULL,
                    Nome VARCHAR(150) NOT NULL,
                    Email VARCHAR(150) UNIQUE NOT NULL,
                    DataRegistro TIMESTAMP NOT NULL DEFAULT NOW(),
                    FOREIGN KEY (AfiliadoId) REFERENCES Afiliados(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS Comissoes (
                    Id SERIAL PRIMARY KEY,
                    ReferidoId INTEGER NOT NULL,
                    Valor DECIMAL(12,2) NOT NULL,
                    DataGerada TIMESTAMP NOT NULL DEFAULT NOW(),
                    Pago BOOLEAN NOT NULL DEFAULT FALSE,
                    FOREIGN KEY (ReferidoId) REFERENCES Referidos(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS Pagamentos (
                    Id SERIAL PRIMARY KEY,
                    AfiliadoId INTEGER NOT NULL,
                    Valor DECIMAL(12,2) NOT NULL,
                    DataPagamento TIMESTAMP NOT NULL DEFAULT NOW(),
                    Metodo VARCHAR(50) NOT NULL,
                    FOREIGN KEY (AfiliadoId) REFERENCES Afiliados(Id) ON DELETE CASCADE
                );
            ";

            using var cmd = new NpgsqlCommand(sql, _connection);
            await cmd.ExecuteNonQueryAsync();
        }

        // ===================================================
        //  MÉTODOS AUXILIARES PARA CONSULTAS E COMANDOS SQL
        // ===================================================

        public async Task<int> ExecuteNonQueryAsync(string sql, params NpgsqlParameter[] parameters)
        {
            using var cmd = new NpgsqlCommand(sql, _connection);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<NpgsqlDataReader> ExecuteQueryAsync(string sql, params NpgsqlParameter[] parameters)
        {
            using var cmd = new NpgsqlCommand(sql, _connection);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            return await cmd.ExecuteReaderAsync();
        }

        public async Task<T> ExecuteScalarAsync<T>(string sql, params NpgsqlParameter[] parameters)
        {
            using var cmd = new NpgsqlCommand(sql, _connection);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            object result = await cmd.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result, typeof(T));
        }

        // =============================
        //   TRANSAÇÕES
        // =============================
        public async Task ExecuteTransactionAsync(Func<NpgsqlTransaction, Task> action)
        {
            using var transaction = await _connection.BeginTransactionAsync();

            try
            {
                await action(transaction);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =============================
        //   DISPOSE
        // =============================
        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}
