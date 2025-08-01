using System;

namespace VideoConversion_ClientTo.Domain.ValueObjects
{
    /// <summary>
    /// STEP-1: 值对象 - 任务标识符
    /// 职责: 封装任务ID的业务规则和验证
    /// </summary>
    public class TaskId : IEquatable<TaskId>
    {
        private readonly string _value;

        private TaskId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Task ID cannot be null or empty", nameof(value));
                
            _value = value;
        }

        public static TaskId Create(string value)
        {
            return new TaskId(value);
        }

        public static TaskId NewId()
        {
            return new TaskId(Guid.NewGuid().ToString());
        }

        public string Value => _value;

        // 相等性比较
        public bool Equals(TaskId? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return _value == other._value;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as TaskId);
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
        public static bool operator ==(TaskId? left, TaskId? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TaskId? left, TaskId? right)
        {
            return !Equals(left, right);
        }

        // 隐式转换
        public static implicit operator string(TaskId taskId)
        {
            return taskId._value;
        }
    }
}
