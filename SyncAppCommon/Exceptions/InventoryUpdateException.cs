using System;

namespace SyncAppCommon.Exceptions
{
    public class InventoryUpdateException : Exception
    {
        public string ErrorMessage { get; set; }

        public InventoryUpdateException(string message) : base(message)
        {
            ErrorMessage = message;
        }
    }
}
