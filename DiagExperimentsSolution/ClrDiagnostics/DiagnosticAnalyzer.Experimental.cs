using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;
using Microsoft.Diagnostics.Symbols;
using System.IO;

namespace ClrDiagnostics
{
    public partial class DiagnosticAnalyzer
    {
        //public IList<PdbInfo> GetAllPdbs()
        //{
        //    var pdbInfos = _dataTarget.EnumerateModules()
        //        .Select(m => m.GetPEImage())
        //        .Where(p => p != null)
        //        .SelectMany(p => p.Pdbs)
        //        .ToList();

        //    return pdbInfos;
        //}

        private ulong GetOffsetForFieldNative(ulong addressBase, string className, string fieldName)
        {
            string pdbFilename = null;
            foreach (var module in _dataTarget.EnumerateModules())
            {
                var image = module.GetPEImage();
                if (image == null) continue;

                var imageBase = module.ImageBase;
                var imageSize = (ulong)module.IndexFileSize;
                if (addressBase > imageBase && addressBase < (imageBase + imageSize))
                {
                    // we found the module
                    pdbFilename = module.Pdb.Path;
                }
            }

            if (pdbFilename == null) return 0;

            using var tw = new StringWriter();
            var reader = new SymbolReader(tw);
            var nativeModule = reader.OpenNativeSymbolFile(pdbFilename);
            //nativeModule.
            //reader.OpenNativeSymbolFile()
            //NativeSymbolModule module = new NativeSymbolModule();
            //_dataTarget.DataReader.

            return 0;
        }


    }
}
