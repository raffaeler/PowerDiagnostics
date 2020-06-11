using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using Microsoft.Diagnostics.Runtime;

namespace ClrDiagnostics.Models
{
    [Obsolete]
    public class AllocatorInfo
    {
        public static readonly AllocatorInfo Default = new AllocatorInfo(true);

        private AllocatorInfo(bool isDefault)
        {
            this.IsDefault = isDefault;
        }

        public static AllocatorInfo Create(ClrObject allocator)
        {
            if (allocator.Address == 0) return Default;

            var instance = new AllocatorInfo(false)
            {
                Allocator = allocator,
            };

            return instance;
        }

        public bool IsDefault { get; }
        public ClrObject Allocator { get; private set; }

        public override int GetHashCode()
        {
            return Allocator.Address.GetHashCode();
        }

        public override string ToString()
        {
            if (IsDefault) return "LoaderAllocator: Default";
            return Allocator.ToString();
        }
    }

    public class AllocatorInfoComparer : IEqualityComparer<AllocatorInfo>
    {
        public bool Equals([AllowNull] AllocatorInfo x, [AllowNull] AllocatorInfo y)
        {
            if(x == null || y == null) return false;
            return x.Allocator.Address.Equals(y.Allocator.Address);
        }

        public int GetHashCode([DisallowNull] AllocatorInfo obj)
        {
            return obj.Allocator.GetHashCode();
        }
    }
}
