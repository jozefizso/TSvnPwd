// This program was cobbled together by Richard Kagerer at Leapbeyond Solutions Inc.
// It is Copyright (c) 2011 Leapbeyond Solutions Inc.
//
// Feel free to use the code however you wish.  If modifying the code or
// using it in your own project, it would be appreciated if you include a
// reference to the original author in your source code.

using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;

class Program {

  const int GENERAL_ERROR = -1;                                     // Error value returned by console program if failed
  const int ERROR_TOOMANY = -2;                                     // Error value returned if only partial output displayed    
  const string AUTHFILE_SUBPATH = @"Subversion\auth\svn.simple";    // Relative path to password files (from %APPDATA%)
  const int MAX_FILES_COUNT = 200;                                  // After this many password files processed, abort

  static void Main(string[] args) {

    bool interactive = true;
    if (args.Length > 0 && args[1].ToUpper() == "-S") interactive = false;  // Silent (no prompt) switch

    try {
      Run();
    } catch (Exception e) {
      Console.WriteLine();
      Console.WriteLine(e.Message);
      Console.WriteLine(e.StackTrace);
    } finally {
      if (interactive) {
        Console.WriteLine();
        Console.WriteLine("Press any key.");
        Console.ReadKey(true);
      }
    }
  }

  static void Run() {
      
    // Show version and introductory info
    Console.WriteLine("TortoiseSVN Password Decrypter v" + Version());
    Console.WriteLine("The original version of this program was created by Leapbeyond Solutions.");

    // Look for password files
    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AUTHFILE_SUBPATH);
    if (!Directory.Exists(folder)) ExitWithError("Path not found: " + folder);
    string[] files = Directory.GetFiles(folder, new String('?', 32)); // Password filenames appear to be 32 characters in length
    if (files.Length < 1) ExitWithError("No files with exactly 32 characters in the filename found in " + folder);

    Console.WriteLine();
    Console.WriteLine(String.Format("Found {0} cached credentials files in {1}", files.Length, folder));

    // Iterate each
    string username = "", repository = "", encryptedPassword = "", decryptedPassword = "";
    for (int i = 0; i < files.Length; i++) {

      if (i > MAX_FILES_COUNT) ExitWithError("Listing aborted.  Too many files in " + folder, ERROR_TOOMANY);

      Console.WriteLine();
      Console.WriteLine("Parsing " + Path.GetFileName(files[i]));

      if (TryParseAuthFile(files[i], out username, out repository, out encryptedPassword)) {
        Console.WriteLine("Repository: " + repository);
        Console.WriteLine("Username: " + username);
        if (TryDecryptPassword(encryptedPassword, out decryptedPassword)) {
          Console.WriteLine("Password: " + decryptedPassword);
        }
      }

    } // end for

  }

  static string Version() {
    var ver = Assembly.GetExecutingAssembly().GetName().Version;
    return string.Format("{0}.{1}.{2}", ver.Major, ver.Minor, ver.Build);
  }

  static void ExitWithError(string error) {
    ExitWithError(error, GENERAL_ERROR);
  }

  static void ExitWithError(string error, int errorCode) {
    Console.WriteLine();
    Console.WriteLine(error);
    Environment.Exit(errorCode);
  }

  static bool TryParseAuthFile(string path, out string username, out string repository, out string encryptedPassword) {

    username = "";
    repository = "";
    encryptedPassword = "";
      
    // Read file and parse key/value pairs
    Dictionary <string, string> results = null;
    try {
      results = AuthFileParser.ReadFile(path);
      if (!results.TryGetValue("username", out username)) return false;
      if (!results.TryGetValue("svn:realmstring", out repository)) return false;
      if (!results.TryGetValue("password", out encryptedPassword)) return false;
      return true;
    } catch (AuthParseException e) {
      Console.WriteLine(e.Message);
      return false;
    }

  }

  static bool TryDecryptPassword(string encrypted, out string decrypted) {
    decrypted = "";
    try {
      decrypted = DPAPI.Decrypt(encrypted);
      return true;
    } catch (Exception) {
      Console.WriteLine("Unable to decrypt the password");
      return false;
    }
  }
        
}
