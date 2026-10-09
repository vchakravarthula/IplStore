using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Domain.Exceptions
{
    public class NotFoundException : DomainException
    {
        public NotFoundException(string resourceName, object key)
            : base($"Entity \"{resourceName}\" ({key}) was not found.") { }

        public NotFoundException(string message) : base(message) { }
    }
}
