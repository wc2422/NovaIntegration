namespace NovaIntegration.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();
            string faceStorageProvider = builder.Configuration["FaceRecognition:StorageProvider"]?.Trim() ?? "Oracle";
            if (string.Equals(faceStorageProvider, "Oracle", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddSingleton<Services.FaceStorage.IFaceEnrollmentRepository,
                    Services.FaceStorage.OracleFaceEnrollmentRepository>();
            }
            else if (string.Equals(faceStorageProvider, "Memory", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddSingleton<Services.FaceStorage.IFaceEnrollmentRepository,
                    Services.FaceStorage.InMemoryFaceEnrollmentRepository>();
            }
            else
            {
                throw new InvalidOperationException("FaceRecognition:StorageProvider must be Memory or Oracle.");
            }
            builder.Services.AddSingleton<Services.FaceRecognitionDemoService>();
            builder.Services.AddSingleton<Services.CameraFrameSourceService>();

            WebApplication app = builder.Build();
            await app.Services.GetRequiredService<Services.FaceRecognitionDemoService>()
                .LoadEnrollmentsAsync(app.Lifetime.ApplicationStopping);

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Dashboard/Error");
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Dashboard}/{action=Index}/{id?}");

            await app.RunAsync();
        }
    }
}
