using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ClrDiagnostics.Extensions;

using Microsoft.Diagnostics.Runtime;

namespace ClrDiagnostics.Models
{
    public class ClrGraphNode
    {
        private ClrGraph _owner;

        internal ClrGraphNode(ClrGraph owner, ClrObject startObject)
        {
            _owner = owner;
            this.Self = startObject;
            if (_owner.Visited.Contains(startObject.Address))
            {
                IsAlreadyVisited = true;
                return;
            }

            PopulateChildren(startObject);
        }

        internal ClrGraphNode(ClrGraph owner, ClrReference startReference)
        {
            _owner = owner;
            this.Self = startReference.Object;
            this.SelfReference = startReference;

            if (_owner.Visited.Contains(startReference.Object.Address))
            {
                IsAlreadyVisited = true;
                return;
            }

            PopulateChildren(startReference.Object);
        }

        private void PopulateChildren(ClrObject @object)
        {
            Children = @object.EnumerateReferencesWithFields()
                .Where(r => !r.Object.IsNull)// && !_owner.Visited.Contains(r.Object.Address))
                .Select(o => new ClrGraphNode(_owner, o));
        }

        public ClrObject Self { get; }
        public ClrReference SelfReference { get; }

        public IEnumerable<ClrGraphNode> Children { get; private set; }

        public bool IsAlreadyVisited { get; }

        public override string ToString()
        {
            if (SelfReference.Equals(default(ClrReference))) return ToStringNoReference();

            var clrObject = SelfReference.Object;
            string val = (clrObject.Type.ElementType == ClrElementType.String && !IsAlreadyVisited)
                ? clrObject.GetStringValue() : "";
            if (SelfReference.Field != null)
            {
                val += $" Field: {SelfReference.Field.Name}";
            }

            return $"Address: 0x{clrObject.Address:X16} Type: {clrObject.Type.Name} Visited: {IsAlreadyVisited} {val}";
        }

        private string ToStringNoReference()
        {
            string val = (Self.Type.ElementType == ClrElementType.String && !IsAlreadyVisited)
                ? Self.GetStringValue() : "";

            return $"Address: 0x{Self.Address:X16} Type: {Self.Type.Name} Visited: {IsAlreadyVisited} {val}";
        }
    }
}
