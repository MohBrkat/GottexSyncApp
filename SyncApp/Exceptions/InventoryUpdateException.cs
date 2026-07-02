using System;

namespace SyncApp.Exceptions
{
    public class InventoryUpdateException : Exception
    {
        public InventoryUpdateException(string message) : base(message)
        {
        }
    }
}