namespace DiagnosticServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var corsPolicy = "CorsPolicy";

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            // Cors configuration is needed during front-end development
            // because react is hosted in a different domain 
            // which is typically localhost:3000
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(corsPolicy, policy =>
                {
                    policy
                        //.AllowAnyOrigin()
                        .WithOrigins("https://localhost:3000", "http://localhost:3000")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseCors(corsPolicy);    // raf
            app.UseRouting();           // raf
            app.UseStaticFiles();       // raf

            app.UseAuthorization();


            //app.MapControllers();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapFallbackToFile("/index.html");
            });

            app.Run();
        }
    }
}