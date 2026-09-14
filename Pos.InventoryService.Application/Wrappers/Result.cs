

namespace Pos.InventoryService.Application.Wrappers
{
    public class Result<T>
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public T? Value { get; }
        public List<string> Errors { get; }

        private Result(bool isSuccess, T? value, List<string> errors)
        {
            IsSuccess = isSuccess;
            Value = value;
            Errors = errors;
        }

        public static Result<T> Success(T value)
        {
            return new Result<T>(true, value, new List<string>());
        }

        public static Result<T> Failure(params string[] errors)
        {
            return new Result<T>(false, default, errors.ToList());
        }
    }
    public class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public List<string> Errors { get; }

        private Result(bool isSuccess, List<string> errors)
        {
            IsSuccess = isSuccess;
            Errors = errors;
        }

        public static Result Success()
        {
            return new Result(true, new List<string>());
        }

        public static Result Failure(params string[] errors)
        {
            return new Result(false, errors.ToList());
        }
    }
}
