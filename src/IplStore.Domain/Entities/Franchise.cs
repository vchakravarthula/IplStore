using IplStore.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Domain.Entities
{
    public class Franchise : Entity, IAggregateRoot
    {
        public string Name { get; private set; } = default!;
        public string Code { get; private set; } = default!; // e.g., "CSK", "MI"
        public string LogoUrl { get; private set; } = default!;

        private Franchise() { } // EF Core

        public Franchise(string name, string code, string logoUrl)
        {
            Name = name;
            Code = code;
            LogoUrl = logoUrl;
        }
    }
}
