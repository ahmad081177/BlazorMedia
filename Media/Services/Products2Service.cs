using System.Data.OleDb;
using MediaModels;

namespace Media.Services
{
    public class Products2Service
    {
        private readonly string _connectionString;

        private sealed class RelationSchema
        {
            public string ProductForeignKeyColumn { get; set; } = string.Empty;
            public string MediaForeignKeyColumn { get; set; } = string.Empty;
            public string RelationPrimaryKeyColumn { get; set; } = string.Empty;
            public string MediaPrimaryKeyColumn { get; set; } = string.Empty;
        }

        public Products2Service(IWebHostEnvironment environment)
        {
            string dbPath = Path.Combine(environment.ContentRootPath, "DB", "Database.accdb");

            if (!File.Exists(dbPath))
            {
                throw new FileNotFoundException($"Database file not found at: {dbPath}");
            }

            _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={dbPath};Persist Security Info=False;";
        }

        public class Product2MediaLink
        {
            public int RelationID { get; set; }
            public int MediaID { get; set; }
            public string MediaURL { get; set; } = string.Empty;
            public bool IsPrimary { get; set; }
        }

        public class Product2Details
        {
            public Products2 Product { get; set; } = new();
            public List<Product2MediaLink> MediaItems { get; set; } = new();
        }

        public class Product2ListItem
        {
            public Products2 Product { get; set; } = new();
            public int MediaCount { get; set; }
            public int VideoCount { get; set; }
            public string? PrimaryMediaUrl { get; set; }
        }

        public async Task<int> CreateAsync(Products2 product, IReadOnlyList<MediaItem> mediaItems, int primaryIndex)
        {
            if (mediaItems.Count == 0)
            {
                throw new InvalidOperationException("At least one media item is required.");
            }

            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            var schema = await ResolveRelationSchemaAsync(connection, transaction);

            try
            {
                int productId;

                using (var insertProduct = new OleDbCommand(
                           "INSERT INTO Products2 (Name, Info) VALUES (@Name, @Info)",
                           connection,
                           transaction))
                {
                    insertProduct.Parameters.AddWithValue("@Name", product.Name);
                    insertProduct.Parameters.AddWithValue("@Info", product.Info ?? string.Empty);
                    await insertProduct.ExecuteNonQueryAsync();

                    insertProduct.CommandText = "SELECT @@IDENTITY";
                    productId = Convert.ToInt32(await insertProduct.ExecuteScalarAsync());
                }

                await InsertMediaRelationsAsync(connection, transaction, schema, productId, mediaItems, primaryIndex);
                transaction.Commit();

                return productId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> UpdateAsync(Products2 product, IReadOnlyList<MediaItem> mediaItems, int primaryIndex)
        {
            if (mediaItems.Count == 0)
            {
                throw new InvalidOperationException("At least one media item is required.");
            }

            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            var schema = await ResolveRelationSchemaAsync(connection, transaction);

            try
            {
                using (var updateProduct = new OleDbCommand(
                           "UPDATE Products2 SET Name = @Name, Info = @Info WHERE ID = @ID",
                           connection,
                           transaction))
                {
                    updateProduct.Parameters.AddWithValue("@Name", product.Name);
                    updateProduct.Parameters.AddWithValue("@Info", product.Info ?? string.Empty);
                    updateProduct.Parameters.AddWithValue("@ID", product.ID);
                    var rows = await updateProduct.ExecuteNonQueryAsync();

                    if (rows == 0)
                    {
                        transaction.Rollback();
                        return false;
                    }
                }

                var oldMediaIds = new List<int>();
                using (var selectOld = new OleDbCommand(
                           $"SELECT [{schema.MediaForeignKeyColumn}] FROM [ProductMediaRel] WHERE [{schema.ProductForeignKeyColumn}] = @ProductID",
                           connection,
                           transaction))
                {
                    selectOld.Parameters.AddWithValue("@ProductID", product.ID);
                    using var reader = (OleDbDataReader)await selectOld.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        oldMediaIds.Add(Convert.ToInt32(reader[schema.MediaForeignKeyColumn]));
                    }
                }

                using (var deleteRels = new OleDbCommand(
                           $"DELETE FROM [ProductMediaRel] WHERE [{schema.ProductForeignKeyColumn}] = @ProductID",
                           connection,
                           transaction))
                {
                    deleteRels.Parameters.AddWithValue("@ProductID", product.ID);
                    await deleteRels.ExecuteNonQueryAsync();
                }

                await InsertMediaRelationsAsync(connection, transaction, schema, product.ID, mediaItems, primaryIndex);

                foreach (var mediaId in oldMediaIds)
                {
                    using var cleanup = new OleDbCommand(
                        $@"DELETE FROM [MediaItem]
                           WHERE [{schema.MediaPrimaryKeyColumn}] = @ID
                             AND NOT EXISTS (SELECT 1 FROM [ProductMediaRel] WHERE [{schema.MediaForeignKeyColumn}] = @ID)",
                        connection,
                        transaction);

                    cleanup.Parameters.AddWithValue("@ID", mediaId);
                    cleanup.Parameters.AddWithValue("@ID", mediaId);
                    await cleanup.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int productId)
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            var schema = await ResolveRelationSchemaAsync(connection, transaction);

            try
            {
                var mediaIds = new List<int>();
                using (var getMedia = new OleDbCommand(
                           $"SELECT [{schema.MediaForeignKeyColumn}] FROM [ProductMediaRel] WHERE [{schema.ProductForeignKeyColumn}] = @ProductID",
                           connection,
                           transaction))
                {
                    getMedia.Parameters.AddWithValue("@ProductID", productId);
                    using var reader = (OleDbDataReader)await getMedia.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        mediaIds.Add(Convert.ToInt32(reader[schema.MediaForeignKeyColumn]));
                    }
                }

                using (var deleteRels = new OleDbCommand(
                           $"DELETE FROM [ProductMediaRel] WHERE [{schema.ProductForeignKeyColumn}] = @ProductID",
                           connection,
                           transaction))
                {
                    deleteRels.Parameters.AddWithValue("@ProductID", productId);
                    await deleteRels.ExecuteNonQueryAsync();
                }

                int deletedRows;
                using (var deleteProduct = new OleDbCommand(
                           "DELETE FROM Products2 WHERE ID = @ID",
                           connection,
                           transaction))
                {
                    deleteProduct.Parameters.AddWithValue("@ID", productId);
                    deletedRows = await deleteProduct.ExecuteNonQueryAsync();
                }

                foreach (var mediaId in mediaIds)
                {
                    using var cleanup = new OleDbCommand(
                                                $@"DELETE FROM [MediaItem]
                                                     WHERE [{schema.MediaPrimaryKeyColumn}] = @ID
                                                         AND NOT EXISTS (SELECT 1 FROM [ProductMediaRel] WHERE [{schema.MediaForeignKeyColumn}] = @ID)",
                        connection,
                        transaction);

                    cleanup.Parameters.AddWithValue("@ID", mediaId);
                    cleanup.Parameters.AddWithValue("@ID", mediaId);
                    await cleanup.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return deletedRows > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<Products2?> GetByIdAsync(int id)
        {
            await using var connection = new OleDbConnection(_connectionString);
            using var command = new OleDbCommand("SELECT ID, Name, Info FROM Products2 WHERE ID = @ID", connection);
            command.Parameters.AddWithValue("@ID", id);

            await connection.OpenAsync();
            using var reader = (OleDbDataReader)await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new Products2
                {
                    ID = Convert.ToInt32(reader["ID"]),
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    Info = reader["Info"]?.ToString() ?? string.Empty
                };
            }

            return null;
        }

        public async Task<List<Products2>> GetAllAsync()
        {
            var products = new List<Products2>();

            await using var connection = new OleDbConnection(_connectionString);
            using var command = new OleDbCommand("SELECT ID, Name, Info FROM Products2 ORDER BY ID DESC", connection);

            await connection.OpenAsync();
            using var reader = (OleDbDataReader)await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                products.Add(new Products2
                {
                    ID = Convert.ToInt32(reader["ID"]),
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    Info = reader["Info"]?.ToString() ?? string.Empty
                });
            }

            return products;
        }

        public async Task<List<Product2MediaLink>> GetMediaForProductAsync(int productId)
        {
            var media = new List<Product2MediaLink>();

            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync();
            var schema = await ResolveRelationSchemaAsync(connection, null);

            using var command = new OleDbCommand(
                $@"SELECT r.[{schema.RelationPrimaryKeyColumn}] AS RelationID,
                          r.[{schema.MediaForeignKeyColumn}] AS MediaID,
                          r.[IsPrimary],
                          m.[MediaURL]
                   FROM [ProductMediaRel] r
                   INNER JOIN [MediaItem] m ON m.[{schema.MediaPrimaryKeyColumn}] = r.[{schema.MediaForeignKeyColumn}]
                   WHERE r.[{schema.ProductForeignKeyColumn}] = @ProductID
                   ORDER BY r.[IsPrimary] DESC, r.[{schema.RelationPrimaryKeyColumn}]", connection);

            command.Parameters.AddWithValue("@ProductID", productId);

            using var reader = (OleDbDataReader)await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                media.Add(new Product2MediaLink
                {
                    RelationID = Convert.ToInt32(reader["RelationID"]),
                    MediaID = Convert.ToInt32(reader["MediaID"]),
                    MediaURL = reader["MediaURL"]?.ToString() ?? string.Empty,
                    IsPrimary = Convert.ToBoolean(reader["IsPrimary"])
                });
            }

            return media;
        }

        public async Task<Product2Details?> GetDetailsAsync(int id)
        {
            var product = await GetByIdAsync(id);
            if (product is null)
            {
                return null;
            }

            var media = await GetMediaForProductAsync(id);
            return new Product2Details
            {
                Product = product,
                MediaItems = media
            };
        }

        public async Task<List<Product2ListItem>> GetListItemsAsync()
        {
            var list = new List<Product2ListItem>();
            var products = await GetAllAsync();

            foreach (var product in products)
            {
                var media = await GetMediaForProductAsync(product.ID);
                var primary = media.FirstOrDefault(m => m.IsPrimary)?.MediaURL ?? media.FirstOrDefault()?.MediaURL;

                list.Add(new Product2ListItem
                {
                    Product = product,
                    MediaCount = media.Count,
                    VideoCount = media.Count(m => IsVideo(m.MediaURL)),
                    PrimaryMediaUrl = primary
                });
            }

            return list;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            await using var connection = new OleDbConnection(_connectionString);
            using var command = new OleDbCommand("SELECT COUNT(*) FROM Products2 WHERE ID = @ID", connection);
            command.Parameters.AddWithValue("@ID", id);

            await connection.OpenAsync();
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            return count > 0;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await using var connection = new OleDbConnection(_connectionString);
                await connection.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsVideo(string mediaUrl)
        {
            var ext = Path.GetExtension(mediaUrl).ToLowerInvariant();
            return ext is ".mp4" or ".avi" or ".mov" or ".wmv" or ".mkv" or ".webm";
        }

        private static async Task InsertMediaRelationsAsync(
            OleDbConnection connection,
            OleDbTransaction transaction,
            RelationSchema schema,
            int productId,
            IReadOnlyList<MediaItem> mediaItems,
            int primaryIndex)
        {
            int normalizedPrimary = primaryIndex < 0 || primaryIndex >= mediaItems.Count ? 0 : primaryIndex;

            for (int index = 0; index < mediaItems.Count; index++)
            {
                var media = mediaItems[index];

                int mediaId;
                using (var insertMedia = new OleDbCommand(
                           "INSERT INTO MediaItem (MediaURL) VALUES (@MediaURL)",
                           connection,
                           transaction))
                {
                    insertMedia.Parameters.AddWithValue("@MediaURL", media.MediaURL);
                    await insertMedia.ExecuteNonQueryAsync();

                    insertMedia.CommandText = "SELECT @@IDENTITY";
                    mediaId = Convert.ToInt32(await insertMedia.ExecuteScalarAsync());
                }

                using var insertRel = new OleDbCommand(
                    $"INSERT INTO [ProductMediaRel] ([{schema.ProductForeignKeyColumn}], [{schema.MediaForeignKeyColumn}], [IsPrimary]) VALUES (@ProductID, @MediaID, @IsPrimary)",
                    connection,
                    transaction);

                insertRel.Parameters.AddWithValue("@ProductID", productId);
                insertRel.Parameters.AddWithValue("@MediaID", mediaId);
                insertRel.Parameters.AddWithValue("@IsPrimary", index == normalizedPrimary);
                await insertRel.ExecuteNonQueryAsync();
            }
        }

        private static async Task<RelationSchema> ResolveRelationSchemaAsync(OleDbConnection connection, OleDbTransaction? transaction)
        {
            var relationColumns = await GetTableColumnNamesAsync(connection, transaction, "ProductMediaRel");
            var mediaColumns = await GetTableColumnNamesAsync(connection, transaction, "MediaItem");

            return new RelationSchema
            {
                ProductForeignKeyColumn = FindFirstExistingColumn(relationColumns, "ProductID", "Products2ID", "Product2ID", "ProductsID"),
                MediaForeignKeyColumn = FindFirstExistingColumn(relationColumns, "MediaID", "MediaItemID", "ItemID"),
                RelationPrimaryKeyColumn = FindFirstExistingColumn(relationColumns, "ID", "RelationID", "ProductMediaRelID"),
                MediaPrimaryKeyColumn = FindFirstExistingColumn(mediaColumns, "ID", "MediaID", "MediaItemID")
            };
        }

        private static async Task<HashSet<string>> GetTableColumnNamesAsync(OleDbConnection connection, OleDbTransaction? transaction, string tableName)
        {
            using var command = new OleDbCommand($"SELECT * FROM [{tableName}] WHERE 1 = 0", connection, transaction);
            using var reader = (OleDbDataReader)await command.ExecuteReaderAsync();

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            return columns;
        }

        private static string FindFirstExistingColumn(HashSet<string> existingColumns, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (existingColumns.Contains(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException($"Could not resolve required DB column. Existing columns: {string.Join(", ", existingColumns)}");
        }
    }
}
