using SqlSugar;
using System;
using System.IO;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// 数据库服务，管理SQLite数据库连接和操作
    /// </summary>
    public class DatabaseService
    {
        private static DatabaseService? _instance;
        private static readonly object _lock = new object();
        private readonly SqlSugarScope _db;

        public static DatabaseService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new DatabaseService();
                        }
                    }
                }
                return _instance;
            }
        }

        private DatabaseService()
        {
            // 获取程序根目录
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var dbPath = Path.Combine(appDirectory, "VideoConversion.db");

            // 配置SqlSugar
            var config = new ConnectionConfig()
            {
                ConnectionString = $"Data Source={dbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            };

            _db = new SqlSugarScope(config);

            // 初始化数据库表
            InitializeDatabase();
        }

        /// <summary>
        /// 获取数据库实例
        /// </summary>
        public SqlSugarScope GetDatabase()
        {
            return _db;
        }

        /// <summary>
        /// 初始化数据库表结构
        /// </summary>
        private void InitializeDatabase()
        {
            try
            {
                // 创建系统设置表
                _db.CodeFirst.InitTables<SystemSettingsEntity>();
                
                System.Diagnostics.Debug.WriteLine("数据库初始化完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"数据库初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取系统设置
        /// </summary>
        public SystemSettingsEntity? GetSystemSettings()
        {
            try
            {
                return _db.Queryable<SystemSettingsEntity>()
                         .OrderByDescending(x => x.UpdateTime)
                         .First();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取系统设置失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 保存系统设置
        /// </summary>
        public bool SaveSystemSettings(SystemSettingsEntity settings)
        {
            try
            {
                // 删除旧设置（保持只有一条记录）
                _db.Deleteable<SystemSettingsEntity>().ExecuteCommand();
                
                // 插入新设置
                settings.UpdateTime = DateTime.Now;
                var result = _db.Insertable(settings).ExecuteCommand();
                
                System.Diagnostics.Debug.WriteLine($"保存系统设置成功，影响行数: {result}");
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存系统设置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 更新系统设置
        /// </summary>
        public bool UpdateSystemSettings(SystemSettingsEntity settings)
        {
            try
            {
                settings.UpdateTime = DateTime.Now;
                var result = _db.Updateable(settings).ExecuteCommand();
                
                System.Diagnostics.Debug.WriteLine($"更新系统设置成功，影响行数: {result}");
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新系统设置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查数据库连接
        /// </summary>
        public bool TestConnection()
        {
            try
            {
                var result = _db.Ado.GetString("SELECT 'OK'");
                return result == "OK";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"数据库连接测试失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取数据库文件路径
        /// </summary>
        public string GetDatabasePath()
        {
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appDirectory, "VideoConversion.db");
        }

        /// <summary>
        /// 获取数据库文件大小
        /// </summary>
        public long GetDatabaseSize()
        {
            try
            {
                var dbPath = GetDatabasePath();
                if (File.Exists(dbPath))
                {
                    var fileInfo = new FileInfo(dbPath);
                    return fileInfo.Length;
                }
                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取数据库大小失败: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 备份数据库
        /// </summary>
        public bool BackupDatabase(string backupPath)
        {
            try
            {
                var dbPath = GetDatabasePath();
                if (File.Exists(dbPath))
                {
                    File.Copy(dbPath, backupPath, true);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"备份数据库失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 恢复数据库
        /// </summary>
        public bool RestoreDatabase(string backupPath)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    var dbPath = GetDatabasePath();
                    File.Copy(backupPath, dbPath, true);
                    
                    // 重新初始化数据库连接
                    InitializeDatabase();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"恢复数据库失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _db?.Dispose();
        }
    }
}
