namespace SpiderHood.Models
{
    /// <summary>
    /// Clase auxiliar para resultados de operaciones
    /// </summary>
    public class OperationResult
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public object? Data { get; }

        private OperationResult(bool isSuccess, string? errorMessage = null, object? data = null)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Data = data;
        }

        public static OperationResult Success(object? data = null)
            => new OperationResult(true, data: data);

        public static OperationResult Failure(string errorMessage)
            => new OperationResult(false, errorMessage);
    }
}
