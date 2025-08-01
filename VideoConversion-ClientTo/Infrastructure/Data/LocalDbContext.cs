using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VideoConversion_ClientTo.Infrastructure.Data.Entities;

namespace VideoConversion_ClientTo.Infrastructure.Data
{
    /// <summary>
    /// 本地数据库上下文
    /// 职责: 管理本地SQLite数据库
    /// </summary>
    public class LocalDbContext : DbContext
    {
        public DbSet<LocalConversionTaskEntity> ConversionTasks { get; set; }
        public DbSet<SystemSettingsEntity> SystemSettings { get; set; }

        public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置LocalConversionTaskEntity
            modelBuilder.Entity<LocalConversionTaskEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TaskId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TaskName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.SourceFilePath).IsRequired().HasMaxLength(500);
                entity.Property(e => e.SourceFileName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.OutputFormat).HasMaxLength(10);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                entity.Property(e => e.LocalFilePath).HasMaxLength(500);
                entity.Property(e => e.ConversionParameters).HasMaxLength(2000);

                // 创建索引
                entity.HasIndex(e => e.TaskId).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);
            });

            // 配置SystemSettingsEntity
            modelBuilder.Entity<SystemSettingsEntity>(entity =>
            {
                entity.HasKey(e => e.Key);
                entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Value).HasMaxLength(2000);
                entity.Property(e => e.Description).HasMaxLength(500);
            });

            // 添加默认设置
            modelBuilder.Entity<SystemSettingsEntity>().HasData(
                new SystemSettingsEntity
                {
                    Key = "ServerUrl",
                    Value = "http://localhost:5065",
                    Description = "服务器地址",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new SystemSettingsEntity
                {
                    Key = "DefaultOutputFormat",
                    Value = "mp4",
                    Description = "默认输出格式",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new SystemSettingsEntity
                {
                    Key = "DefaultOutputLocation",
                    Value = "SameAsSource",
                    Description = "默认输出位置",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new SystemSettingsEntity
                {
                    Key = "AutoStartConversion",
                    Value = "false",
                    Description = "自动开始转换",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            );
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbPath = GetDatabasePath();
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
                Utils.Logger.Info("LocalDbContext", $"📁 数据库路径: {dbPath}");
            }
        }

        private string GetDatabasePath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = System.IO.Path.Combine(appDataPath, "VideoConversion-ClientTo");
            
            if (!System.IO.Directory.Exists(appFolder))
            {
                System.IO.Directory.CreateDirectory(appFolder);
            }

            return System.IO.Path.Combine(appFolder, "VideoConversion.db");
        }

        /// <summary>
        /// 确保数据库已创建
        /// </summary>
        public async Task EnsureDatabaseCreatedAsync()
        {
            try
            {
                await Database.EnsureCreatedAsync();
                Utils.Logger.Info("LocalDbContext", "✅ 数据库已确保创建");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("LocalDbContext", $"❌ 创建数据库失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 迁移数据库到最新版本
        /// </summary>
        public async Task MigrateDatabaseAsync()
        {
            try
            {
                await Database.MigrateAsync();
                Utils.Logger.Info("LocalDbContext", "✅ 数据库迁移完成");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("LocalDbContext", $"❌ 数据库迁移失败: {ex.Message}");
                throw;
            }
        }
    }
}
