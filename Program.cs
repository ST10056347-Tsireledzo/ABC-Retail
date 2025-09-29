using ABC_Retail.Services;
using ABC_Retail.Services.Logging.Core;
using ABC_Retail.Services.Logging.Domains.Orders;
using ABC_Retail.Services.Logging.Domains.Products;
using ABC_Retail.Services.Logging.File_Logging;
using ABC_Retail.Services.Queues;
using Azure.Data.Tables;
using Azure.Storage.Blobs;

namespace ABC_Retail
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Load configuration from appsettings.json
            var blobConnection = builder.Configuration["AzureStorage:BlobConnectionString"];
            var containerName = builder.Configuration["AzureStorage:ContainerName"];
            var sqlConnection = builder.Configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(blobConnection))
                throw new Exception("AzureStorage:BlobConnectionString not found in appsettings.json.");

            if (string.IsNullOrWhiteSpace(containerName))
                throw new Exception("AzureStorage:ContainerName not found in appsettings.json.");

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddHttpContextAccessor();

            // Register Blob + Table services using appsettings.json
            builder.Services.AddSingleton(new BlobServiceClient(blobConnection));
            TableServiceClient tableServiceClient = new TableServiceClient(blobConnection);
            builder.Services.AddSingleton(tableServiceClient);

            // Queue Services
            builder.Services.AddSingleton(new ImageUploadQueueService(blobConnection, "image-upload-queue"));
            builder.Services.AddSingleton(new OrderPlacedQueueService(blobConnection, "order-placed-queue"));
            builder.Services.AddSingleton(new ProductQueueService(blobConnection, "product-updates-queue"));
            builder.Services.AddSingleton(new StockReminderQueueService(blobConnection, "stock-reminder-queue"));

            // Core Domain Services
            builder.Services.AddSingleton(sp =>
            {
                var tableClient = sp.GetRequiredService<TableServiceClient>();
                var productQueue = sp.GetRequiredService<ProductQueueService>();
                return new ProductService(tableClient, productQueue);
            });

            builder.Services.AddSingleton(new CustomerService(tableServiceClient));
            builder.Services.AddSingleton(new CartService(tableServiceClient));
            builder.Services.AddSingleton(new AdminService(tableServiceClient));
            builder.Services.AddScoped<BlobImageService>();

            // Logging Services
            builder.Services.AddSingleton<ILogReader, FileLogReader>();
            builder.Services.AddSingleton<OrderLogService>();

            builder.Services.AddSingleton(sp =>
            {
                var orderQueueService = sp.GetRequiredService<OrderPlacedQueueService>();
                var stockReminderQueueService = sp.GetRequiredService<StockReminderQueueService>();
                var orderLogService = sp.GetRequiredService<OrderLogService>();
                return new OrderService(tableServiceClient, orderQueueService, stockReminderQueueService, orderLogService);
            });

            builder.Services.AddSingleton<ILogPathResolver>(sp =>
            {
                var logBasePath = builder.Configuration["LogBasePath"] ?? @"C:\Logs\ABC_Retail";
                return new FileLogPathResolver(logBasePath);
            });

            builder.Services.AddSingleton<ILogWriter, FileLogWriter>();
            builder.Services.AddScoped<ProductLogService>();

            // Sessions
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            var app = builder.Build();

            // ✅ Seed Admin user into Azure Table Storage
            using (var scope = app.Services.CreateScope())
            {
                var seededTableService = scope.ServiceProvider.GetRequiredService<TableServiceClient>();
                var adminTable = seededTableService.GetTableClient("Admins");

                adminTable.CreateIfNotExists();

                var adminEntity = new TableEntity("AdminPartition", "admin@example.com")
                {
                    { "Password", "Admin123!" }, // ⚠️ For demo only (use hashing in production)
                    { "Role", "Admin" }
                };

                try
                {
                    adminTable.AddEntity(adminEntity);
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 409)
                {
                    // Already exists, ignore
                }
            }

            // Middleware pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseSession();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
