// $Id: AssemblyResolver.cs 7552 2019-11-27 12:02:31Z onuchin $
//
// Copyright (C) 2018 Valeriy Onuchin

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace gep
{
   /// <summary>
   ///   Class AssemblyResolver allows to embed  DLLs into the main assembly
   /// </summary>
   internal static class Resolver
   {
      #region Static fields

      private static string tempFolder = "";

      #endregion

      #region Public methods

      /// <summary>
      ///   Extract DLLs from resources to temporary folder
      /// </summary>
      /// <param name="dllName">name of DLL file to create (including dll suffix)</param>
      /// <param name="resourceBytes">The resource name (fully qualified)</param>
      public static void ExtractEmbeddedDlls(string dllName, byte[] resourceBytes)
      {
         var assem = Assembly.GetExecutingAssembly();
         var names = assem.GetManifestResourceNames();
         var an = assem.GetName();

         // The temporary folder holds one or more of the temporary DLLs
         // It is made "unique" to avoid different versions of the DLL or architectures.
         tempFolder = string.Format("{0}.{1}.{2}", an.Name, an.ProcessorArchitecture, an.Version);

         var dirName = Path.Combine(Path.GetTempPath(), tempFolder);

         if (!Directory.Exists(dirName)) {
            Directory.CreateDirectory(dirName);
         }

         // Add the temporary dirName to the PATH environment variable (at the head!)
         var path = Environment.GetEnvironmentVariable("PATH");
         var pathPieces = path.Split(';');
         var found = false;

         foreach (var pathPiece in pathPieces) {
            if (pathPiece == dirName) {
               found = true;
               break;
            }
         }

         if (!found) {
            Environment.SetEnvironmentVariable("PATH", dirName + ";" + path);
         }

         // See if the file exists, avoid rewriting it if not necessary
         var dllPath = Path.Combine(dirName, dllName);
         var rewrite = true;

         if (File.Exists(dllPath)) {
            var existing = File.ReadAllBytes(dllPath);
            if (resourceBytes.SequenceEqual(existing)) {
               rewrite = false;
            }
         }
         if (rewrite) {
            File.WriteAllBytes(dllPath, resourceBytes);
         }

         //SetDllDirectory(dllPath);
         LoadDll(dllPath);
      }

      public static void LoadUnmanaged(string assemblyName)
      {
         try {
            var assembly = Assembly.GetExecutingAssembly();
            var exe = assembly.ToString();
            exe = exe.Substring(0, exe.IndexOf(",", StringComparison.Ordinal));

            var assemblyPath = exe + "." + assemblyName;
            var stream = assembly.GetManifestResourceStream(assemblyPath);

            if (stream == null) {
               return;
            }

            var assemblyRawBytes = new byte[stream.Length];

            stream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
            stream.Dispose();

            ExtractEmbeddedDlls(assemblyName, assemblyRawBytes);
         } catch (Exception ex) {
            Console.WriteLine(MethodBase.GetCurrentMethod() + " : " + ex.Message);
            // MessageBox.Show("LoadAssembly: " + ex);
         }
      }

      /// <summary>
      /// </summary>
      /// <param name="assemblyName"></param>
      /// <returns></returns>
      public static Assembly LoadAssembly(string assemblyName)
      {
         try {
            var assembly = Assembly.GetExecutingAssembly();
            var exe = assembly.ToString();
            exe = exe.Substring(0, exe.IndexOf(",", StringComparison.Ordinal));

            var assemblyPath = exe + "." + assemblyName;
            var stream = assembly.GetManifestResourceStream(assemblyPath);

            if (stream == null) {
               return null;
            }

            var assemblyRawBytes = new byte[stream.Length];

            stream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
            stream.Dispose();

            return Assembly.Load(assemblyRawBytes);
         } catch (Exception ex) {
            Console.WriteLine(MethodBase.GetCurrentMethod() + " : " + ex.Message);
            // MessageBox.Show("LoadAssembly: " + ex);
            return null;
         }
      }

      /// <summary>
      ///   managed wrapper around LoadLibrary
      /// </summary>
      /// <param name="dllName"></param>
      public static void LoadDll(string dllName)
      {
         if (tempFolder == "") {
            throw new Exception("Please call ExtractEmbeddedDlls before LoadDll");
         }
         var h = LoadLibrary(dllName);
         if (h == IntPtr.Zero) {
            Exception e = new Win32Exception();
            throw new DllNotFoundException("Unable to load library: " + dllName + " from " + tempFolder, e);
         }
      }

      /// <summary>
      ///   Resolves the assembly.
      /// </summary>
      public static void ResolveAssembly()
      {
         AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;
         AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
      }

      #endregion

      #region Private methods

      /// <summary>
      ///   Handles the UnhandledException event of the CurrentDomain control.
      /// </summary>
      /// <param name="sender">The source of the event.</param>
      /// <param name="e">The <see cref="UnhandledExceptionEventArgs" /> instance containing the event data.</param>
      private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
      {
         var ex = (Exception) e.ExceptionObject;
         MessageBox.Show(ex.ToString());
         Environment.Exit(Marshal.GetHRForException(ex));
      }

      [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
      private static extern IntPtr LoadLibrary(string lpFileName);

      [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
      [return: MarshalAs(UnmanagedType.Bool)]
      static extern bool SetDllDirectory(string lpPathName);

      /// <summary>
      ///   Called when [resolve assembly].
      /// </summary>
      /// <param name="sender">The sender.</param>
      /// <param name="args">The <see cref="ResolveEventArgs" /> instance containing the event data.</param>
      /// <returns>Assembly.</returns>
      private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
      {
         var executingAssembly = Assembly.GetExecutingAssembly();
         var assemblyName = new AssemblyName(args.Name);

         var exe = executingAssembly.ToString();
         exe = exe.Substring(0, exe.IndexOf(",", StringComparison.Ordinal));

         var path = exe + "." + assemblyName.Name + ".dll";

         if (assemblyName.CultureInfo.Equals(CultureInfo.InvariantCulture) == false) {
            path = string.Format(@"{0}\{1}", assemblyName.CultureInfo, path);
         }

         var stream = executingAssembly.GetManifestResourceStream(path);

         if (stream == null) {
            // try another extension
            path = exe + "." + assemblyName.Name + ".DLL";

            if (assemblyName.CultureInfo.Equals(CultureInfo.InvariantCulture) == false) {
               path = string.Format(@"{0}\{1}", assemblyName.CultureInfo, path);
            }

            stream = executingAssembly.GetManifestResourceStream(path);
            if (stream == null) {
               return null;
            }
         }

         var assemblyRawBytes = new byte[stream.Length];
         stream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
         stream.Dispose();

         return Assembly.Load(assemblyRawBytes);
      }

      #endregion
   }
}
