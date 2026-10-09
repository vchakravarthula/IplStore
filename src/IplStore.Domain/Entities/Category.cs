using IplStore.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Domain.Entities
{
    public class Category : Entity, IAggregateRoot
    {
        public string Name { get; private set; } = default!;
        public string Slug { get; private set; } = default!;

        private Category() { }

        public Category(string name, string slug)
        {
            Name = name;
            Slug = slug;
        }
    }
}
