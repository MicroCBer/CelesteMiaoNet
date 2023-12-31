﻿#if NETCORE
using Celeste.Mod.CelesteNet.DataTypes;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.CelesteNet.Server {
    public partial class CelesteNetServerModuleWrapper {

        private AssemblyLoadContext? ALC;

        private void LoadAssembly() {
            long stamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

            string dir = Path.Combine(Path.GetTempPath(), $"CelesteNetServerModuleCache.{Server.Timestamp}");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(AssemblyPath)}.{stamp}.dll");
            File.Copy(AssemblyPath, path);

            if (File.Exists(Path.ChangeExtension(AssemblyPath, "pdb")))
                File.Copy(Path.ChangeExtension(AssemblyPath, "pdb"), Path.ChangeExtension(path, "pdb"));

            ALC = new ModuleAssemblyLoadContext($"ModCtx.{stamp}.{ID}");
            ALC.Resolving += (ctx, name) => {
                foreach (CelesteNetServerModuleWrapper wrapper in Server.ModuleWrappers)
                    if (wrapper.ID == name.Name)
                        return wrapper.Assembly;
                AssemblyLoadContext? parent = AssemblyLoadContext.GetLoadContext(typeof(CelesteNetServerModuleWrapper).Assembly);
                if (parent != null)
                    foreach (Assembly asm in parent.Assemblies)
                        if (asm.GetName().Name == name.Name)
                            return asm;
                return null;
            };

            Assembly = ALC.LoadFromAssemblyPath(path);
        }

        private void UnloadAssembly() {
            ALC?.Unload();
            ALC = null;
        }

        public class ModuleAssemblyLoadContext : AssemblyLoadContext {

            public ModuleAssemblyLoadContext(string name)
                : base(name, isCollectible: true) {
            }

            protected override Assembly? Load(AssemblyName name) {
                return null;
            }

        }

    }
}
#endif
