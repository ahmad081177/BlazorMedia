using System;
using System.Data.OleDb;
using System.Threading.Tasks;
using MediaModels;

namespace Media.Services
{
    public class ProductsService
    {
        private readonly string _connectionString;

        // Constructor with IWebHostEnvironment injection
        public ProductsService(IWebHostEnvironment environment)
        {
            // Get the database path using WebHostEnvironment
            string dbPath = Path.Combine(environment.ContentRootPath,  "DB", "Database.accdb");

            // Ensure the database file exists
            if (!File.Exists(dbPath))
            {
                throw new FileNotFoundException($"Database file not found at: {dbPath}");
            }

            // Create connection string for MS Access
            _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={dbPath};Persist Security Info=False;";
        }

        #region CRUD Operations

        // CREATE - Add a new product (Async)
        public async Task<int> CreateAsync(Products product)
        {
            string query = @"INSERT INTO Products (Name, Info, Image1, Image2, Video) 
                           VALUES (@Name, @Info, @Image1, @Image2, @Video)";

            using (OleDbConnection connection = new OleDbConnection(_connectionString))
            {
                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Name", product.Name);
                    command.Parameters.AddWithValue("@Info", product.Info ?? string.Empty);
                    command.Parameters.AddWithValue("@Image1", product.Image1);
                    command.Parameters.AddWithValue("@Image2", product.Image2 ?? string.Empty);
                    command.Parameters.AddWithValue("@Video", product.Video ?? string.Empty);
                    try
                    {
                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        // Log the exception (you can use a logging framework here)
                        Console.WriteLine($"Error creating product: {ex.Message}");
                        throw; // Re-throw the exception after logging
                    }
                    // Get the last inserted ID
                    command.CommandText = "SELECT @@IDENTITY";
                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }

        // READ - Get a product by ID (Async)
        public async Task<Products> GetByIdAsync(int id)
        {
            string query = "SELECT * FROM Products WHERE ID = @ID";

            using (OleDbConnection connection = new OleDbConnection(_connectionString))
            {
                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ID", id);

                    await connection.OpenAsync();
                    using (OleDbDataReader reader = (OleDbDataReader)await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapReaderToProduct(reader);
                        }
                    }
                }
            }

            return null;
        }

        // READ - Get all products (Async)
        public async Task<List<Products>> GetAllAsync()
        {
            List<Products> products = new List<Products>();
            string query = "SELECT * FROM Products ORDER BY ID";

            using (OleDbConnection connection = new OleDbConnection(_connectionString))
            {
                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    await connection.OpenAsync();
                    using (OleDbDataReader reader = (OleDbDataReader)await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            products.Add(MapReaderToProduct(reader));
                        }
                    }
                }
            }

            return products;
        }

        // READ - Search products by name (Async)
        public async Task<List<Products>> SearchByNameAsync(string searchTerm)
        {
            List<Products> products = new List<Products>();
            string query = "SELECT * FROM Products WHERE Name LIKE @SearchTerm ORDER BY Name";

            using (OleDbConnection connection = new OleDbConnection(_connectionString))
            {
                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SearchTerm", $"%{searchTerm}%");

                    await connection.OpenAsync();
                    using (OleDbDataReader reader = (OleDbDataReader)await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            products.Add(MapReaderToProduct(reader));
                        }
                    }
                }
            }

            return products;
        }

        // UPDATE - Update an existing product (Async)
        public async Task<bool> UpdateAsync(Products product)
        {
            string query = @"UPDATE Products 
                           SET Name = @Name, 
                               Info = @Info, 
                               Image1 = @Image1, 
                               Image2 = @Image2, 
                               Video = @Video 
                           WHERE ID = @ID";

            using (OleDbConnection connection = new OleDbConnection(_connectionString))
            {
                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Name", product.Name);
                    command.Parameters.AddWithValue("@Info", product.Info ?? string.Empty);
                    command.Parameters.AddWithValue("@Image1", product.Image1);
                    command.Parameters.AddWithValue("@Image2", product.Image2 ?? string.Empty);
                    command.Parameters.AddWithValue("@Video", product.Video ?? string.Empty);
                    command.Parameters.AddWithValue("@ID", product.ID);

                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        // DELETE - Delete a product by ID (Async)
        public async Task<bool> DeleteAsync(int id)
        {
            string query = "DELETE FROM Products WHERE ID = @ID";

            using (OleDbConnection connection = new OleDbConnection(_connectionString))
            {
                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ID", id);

                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        // DELETE - Delete all products (Async - use with caution!)
        public async Task<int> DeleteAllAsync()
        {
            string query = "DELETE FROM Products";

            using (OleDbConnection connection = new OleDbConnection(_connectionString))
            {
                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    await connection.OpenAsync();
                    return await command.ExecuteNonQueryAsync();
                }
            }
        }

        // Check if a product exists (Async)
        public async Task<bool> ExistsAsync(int id)
        {
            string query = "SELECT COUNT(*) FROM Products WHERE ID = @ID";

            using (OleDbConnection connection = new OleDbConnection(_connectionString))
            {
                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ID", id);

                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    int count = Convert.ToInt32(result);
                    return count > 0;
                }
            }
        }

        #endregion

        #region Helper Methods

        // Helper method to map DataReader to Products object
        private Products MapReaderToProduct(OleDbDataReader reader)
        {
            return new Products
            {
                ID = Convert.ToInt32(reader["ID"]),
                Name = reader["Name"].ToString(),
                Info = reader["Info"]?.ToString() ?? string.Empty,
                Image1 = reader["Image1"].ToString(),
                Image2 = reader["Image2"]?.ToString() ?? string.Empty,
                Video = reader["Video"]?.ToString() ?? string.Empty
            };
        }

        // Test database connection (Async)
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using (OleDbConnection connection = new OleDbConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}