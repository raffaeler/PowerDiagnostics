using DiagnosticInvestigations;
using DiagnosticInvestigations.Configurations;

using DiagnosticModels.Converters;

using DiagnosticServer.Hubs;
using DiagnosticServer.Services;

namespace DiagnosticServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var corsPolicy = "CorsPolicy";

            var builder = WebApplication.CreateBuilder(args);

            var generalSection = builder.Configuration.GetSection("General");
            //var generalConfiguration = generalSection.Get<GeneralConfiguration>();
            builder.Services.Configure<GeneralConfiguration>(generalSection);

            builder.Services.AddControllers()
                .AddJsonOptions(options => SetupConverters.ConfigureOptions(options.JsonSerializerOptions));

            builder.Services.AddSignalR();
            // Cors configuration is needed during front-end development
            // because react is hosted in a different domain 
            // which is typically localhost:3000
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(corsPolicy, policy =>
                {
                    policy
                        //.AllowAnyOrigin()
                        .AllowCredentials()
                        .WithOrigins("https://localhost:3000", "http://localhost:3000")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            //builder.Services.AddHostedService<DebuggingSessionService>();
            builder.Services.AddSingleton<DebuggingSessionService>();
            builder.Services.AddHostedService<DebuggingSessionService>(
                provider => provider.GetService<DebuggingSessionService>() ?? throw new Exception($"Service not found ({nameof(DebuggingSessionService)})"));
            builder.Services.AddSingleton<QueriesService>();
            builder.Services.AddSingleton<InvestigationState>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            //app.UseHttpsRedirection();    // testing
            app.UseCors(corsPolicy);    // raf
            app.UseRouting();           // raf
            app.UseStaticFiles();       // raf

            app.UseAuthorization();


            //app.MapControllers();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapFallbackToFile("/index.html");
                endpoints.MapHub<DiagnosticHub>("/diagnosticHub", options =>
                {
                });
            });

            app.Run();
        }
    }
}