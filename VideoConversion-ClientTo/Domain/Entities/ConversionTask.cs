using System;
using VideoConversion_ClientTo.Domain.ValueObjects;
using VideoConversion_ClientTo.Domain.Enums;

namespace VideoConversion_ClientTo.Domain.Entities
{
    /// <summary>
    /// STEP-1: 领域模型 - 转换任务核心实体
    /// 职责: 纯业务逻辑，不依赖任何基础设施
    /// </summary>
    public class ConversionTask
    {
        // 私有构造函数，确保通过工厂方法创建
        private ConversionTask(
            TaskId id,
            TaskName name,
            FileInfo sourceFile,
            ConversionParameters parameters)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            SourceFile = sourceFile ?? throw new ArgumentNullException(nameof(sourceFile));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Status = TaskStatus.Pending;
            CreatedAt = DateTime.UtcNow;
        }

        // 核心属性
        public TaskId Id { get; }
        public TaskName Name { get; }
        public FileInfo SourceFile { get; }
        public ConversionParameters Parameters { get; }
        public TaskStatus Status { get; private set; }
        public int Progress { get; private set; }
        public string? ErrorMessage { get; private set; }
        
        // 时间戳
        public DateTime CreatedAt { get; }
        public DateTime? StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        
        // 性能指标
        public double? ConversionSpeed { get; private set; }
        public TimeSpan? EstimatedTimeRemaining { get; private set; }

        // 兼容性属性
        public double? Speed => ConversionSpeed;
        public TimeSpan? EstimatedRemaining => EstimatedTimeRemaining;

        // 工厂方法
        public static ConversionTask Create(
            string taskName,
            string filePath,
            long fileSize,
            ConversionParameters parameters)
        {
            var id = TaskId.NewId();
            var name = TaskName.Create(taskName);
            var sourceFile = FileInfo.Create(filePath, fileSize);
            
            return new ConversionTask(id, name, sourceFile, parameters);
        }

        // 业务方法
        public void Start()
        {
            if (Status != TaskStatus.Pending)
                throw new InvalidOperationException($"Cannot start task in {Status} status");
                
            Status = TaskStatus.Converting;
            StartedAt = DateTime.UtcNow;
            Progress = 0;
        }

        public void UpdateProgress(int progress, double? speed = null, TimeSpan? eta = null)
        {
            if (Status != TaskStatus.Converting)
                throw new InvalidOperationException($"Cannot update progress for task in {Status} status");
                
            if (progress < 0 || progress > 100)
                throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100");
                
            Progress = progress;
            ConversionSpeed = speed;
            EstimatedTimeRemaining = eta;
        }

        public void Complete()
        {
            if (Status != TaskStatus.Converting)
                throw new InvalidOperationException($"Cannot complete task in {Status} status");
                
            Status = TaskStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            Progress = 100;
            EstimatedTimeRemaining = null;
        }

        public void Fail(string errorMessage)
        {
            if (Status.IsTerminal())
                throw new InvalidOperationException($"Cannot fail task in {Status} status");
                
            Status = TaskStatus.Failed;
            CompletedAt = DateTime.UtcNow;
            ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
            EstimatedTimeRemaining = null;
        }

        public void Cancel()
        {
            if (!CanCancel)
                throw new InvalidOperationException($"Cannot cancel task in {Status} status");
                
            Status = TaskStatus.Cancelled;
            CompletedAt = DateTime.UtcNow;
            EstimatedTimeRemaining = null;
        }

        // 查询方法
        public bool IsCompleted => Status == TaskStatus.Completed;
        public bool IsFailed => Status == TaskStatus.Failed;
        public bool IsInProgress => Status == TaskStatus.Converting;
        public bool CanStart => Status == TaskStatus.Pending;
        public bool CanCancel => Status == TaskStatus.Pending || Status == TaskStatus.Converting;

        public TimeSpan? GetDuration()
        {
            if (StartedAt == null) return null;
            var endTime = CompletedAt ?? DateTime.UtcNow;
            return endTime - StartedAt.Value;
        }

        public string GetStatusDisplay()
        {
            return Status.GetDisplayName();
        }

        public string GetStatusIcon()
        {
            return Status.GetStatusIcon();
        }

        // UI绑定属性
        public string StatusDisplay => GetStatusDisplay();
        public string StatusIcon => GetStatusIcon();
    }
}
