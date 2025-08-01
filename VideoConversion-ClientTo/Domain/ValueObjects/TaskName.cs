using System;

namespace VideoConversion_ClientTo.Domain.ValueObjects
{
    /// <summary>
    /// STEP-1: 值对象 - 任务名称
    /// 职责: 封装任务名称的业务规则和验证
    /// </summary>
    public class TaskName : IEquatable<TaskName>
    {
        private readonly string _value;

        private TaskName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Task name cannot be null or empty", nameof(value));
                
            if (value.Length > 200)
                throw new ArgumentException("Task name cannot exceed 200 characters", nameof(value));
                
            _value = value.Trim();
        }

        public static TaskName Create(string value)
        {
            return new TaskName(value);
        }

        public string Value => _value;

        // 相等性比较
        public bool Equals(TaskName? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return _value == other._value;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as TaskName);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public override string ToString()
        {
            return _value;
        }

        // 操作符重载
        public static bool operator ==(TaskName? left, TaskName? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TaskName? left, TaskName? right)
        {
            return !Equals(left, right);
        }

        // 隐式转换
        public static implicit operator string(TaskName taskName)
        {
            return taskName._value;
        }
    }
}
