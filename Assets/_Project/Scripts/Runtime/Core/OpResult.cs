namespace BusTycoon.Core
{
    /// <summary>
    /// Oyuncu eylemlerinin (otobüs al, hat aç, fiyat değiştir...) sonucu.
    /// Başarısızlıkta <see cref="Message"/> doğrudan UI'da gösterilebilecek Türkçe bir açıklamadır.
    /// </summary>
    public readonly struct OpResult
    {
        public readonly bool Success;
        public readonly string Message;

        OpResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static OpResult Ok(string message = null) => new OpResult(true, message);
        public static OpResult Fail(string message) => new OpResult(false, message);

        public static implicit operator bool(OpResult result) => result.Success;

        public override string ToString() => Success ? $"OK {Message}" : $"FAIL {Message}";
    }
}
