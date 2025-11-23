using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using McpMemoryServer.Data;
using McpMemoryServer.Services;
using McpMemoryServer.Mcp;
using System.Text;

namespace McpMemoryServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Load configuration
            builder.Configuration.AddEnvironmentVariables();
            builder.Configuration.AddJsonFile("appsettings.json", optional: true);

            // Configure services
            ConfigureServices(builder.Services, builder.Configuration);

            var app = builder.Build();

            // Configure middleware pipeline
            ConfigureMiddleware(app);

            // Initialize database
            InitializeDatabase(app);

            app.Run();
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Database
            services.AddDbContext<MemoryDbContext>(options =>
                options.UseSqlite(configuration.GetConnectionString("DefaultConnection") ?? "Data Source=memory.db"));

            // OAuth 2.1 / JWT Authentication
            var jwtKey = configuration["Jwt:Key"] ?? "your-super-secret-key-change-this-in-production-min-32-chars";
            var key = Encoding.ASCII.GetBytes(jwtKey);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // Set to true in production
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "McpMemoryServer",
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"] ?? "McpMemoryClient",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("MemoryRead", policy => policy.RequireClaim("scope", "memory.read"));
                options.AddPolicy("MemoryWrite", policy => policy.RequireClaim("scope", "memory.write"));
                options.AddPolicy("FilesystemTools", policy => policy.RequireClaim("scope", "tools.filesystem"));
            });

            // Services
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IMemoryService, MemoryService>();
            services.AddScoped<IVectorStoreService, VectorStoreService>();
            services.AddScoped<IFileSystemService, FileSystemService>();
            services.AddScoped<IMcpService, McpService>();
            services.AddSingleton<ISessionManager, SessionManager>();

            // Controllers
            services.AddControllers();

            // Swagger/OpenAPI
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "MCP Memory Server API",
                    Version = "v1",
                    Description = "Persistent Memory API Server with Model Context Protocol (MCP) support for ChatGPT",
                    Contact = new OpenApiContact
                    {
                        Name = "MCP Memory Server",
                        Url = new Uri("https://github.com/modelcontextprotocol")
                    }
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // CORS
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
        }

        private static void ConfigureMiddleware(WebApplication app)
        {
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MCP Memory Server API v1");
                    c.RoutePrefix = string.Empty; // Serve Swagger at root
                });
            }

            app.UseCors();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
        }

        private static void InitializeDatabase(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var context = services.GetRequiredService<MemoryDbContext>();
                context.Database.EnsureCreated();

                // Initialize vector store
                var vectorStore = services.GetRequiredService<IVectorStoreService>();
                vectorStore.InitializeAsync().Wait();
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred while initializing the database.");
            }
        }
    }
}
