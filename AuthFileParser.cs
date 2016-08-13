// A simple, naive parser to read a limited subset of Subversion configuration files.
// This class does not understand the full range of syntax for subversion files and
// will fail on files that are in an unexpected - yet legitimate - format.
//
// The files containing cached credentials, as created by TortoiseSVN on Windows, appear
// to contain key value pairs that are broken up into multiple lines.  The logic below is
// based on a brief examination of a single "svn.simple" file.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

internal class AuthFileParser {        

  // Set a limit on maximum lines parsed to avoid stalling out on big files
  const int MAX_LINES = 1000;

  // Parser states
  enum States {
    ExpectingKeyDef,
    ExpectingKeyName,
    ExpectingValueDef,
    ExpectingValue
  }

  // Current state
  private States state = States.ExpectingKeyDef;

  // Data persisted between states
  private string keyName = "";
  private int nextLength = -1;

  // Values read so far
  private Dictionary<string, string> props = new Dictionary<string, string>();

  // Only allow access through static ReadFile() method
  private AuthFileParser() {}

  private bool tryParseNextLine(string line) {

    switch (state) {
      case States.ExpectingKeyDef:    return parseKeyDef(line);
      case States.ExpectingKeyName:   return parseKeyName(line);
      case States.ExpectingValueDef:  return parseValueDef(line);
      case States.ExpectingValue:     return parseValue(line);
      default:                        return false;
    }

  }

  private bool parseKeyDef(string line) {      
    if (!parseDefLine("K", line)) return false;
    state = States.ExpectingKeyName;
    return true;
  }

  private bool parseKeyName(string line) {
    if (!parseValLine(line)) return false;
    state = States.ExpectingValueDef;
    return true;
  }

  private bool parseValueDef(string line) {
    if (!parseDefLine("V", line)) return false;
    state = States.ExpectingValue;
    return true;
  }

  private bool parseValue(string line) {
    if (!parseValLine(line)) return false;
    state = States.ExpectingKeyDef;
    return true;
  }

  // Do some rudimentary validation to ensure the current line looks like a definition
  // line, then parse it.  A definition line looks something like "K #" or "V #",
  // where # is the length of the next line.  K means the next line will be a key name,
  // while V means it will be a value.  # will be stored in nextLength.
  private bool parseDefLine(string prefix, string line) {
    line = line.Trim();
    if (!line.ToUpper().StartsWith(prefix + " ")) return false;
    string[] parts = line.Split(' ');
    if (parts.Length != 2) return false;
    if (!int.TryParse(parts[1], out nextLength)) return false;
    return true;
  }

  // Read a key name or value line.  If this is a value line, then save the key/value
  // pair that has just been read.
  private bool parseValLine(string line) {

    if (line.Length < nextLength) return false;
    string val = line.Substring(0, nextLength);
    nextLength = -1;

    if (state == States.ExpectingKeyName) {
      keyName = val.Trim();
      if (keyName == "") return false;
      if (keyName.Contains(" ")) return false;
    } else {
      props.Add(keyName, val);
      keyName = "";
    }       

    return true;
  }

  public static Dictionary<string, string> ReadFile(string path) {
    AuthFileParser parser = new AuthFileParser();
    using (StreamReader rd = File.OpenText(path)) {        
        
      int lineNum = 1;
      string line = rd.ReadLine();
      while (line != null) {

        if (lineNum > MAX_LINES) break;

        // Skip comment lines
        if (!line.Trim().StartsWith("#")) {

          // Check for end of file marker
          if (parser.state == States.ExpectingKeyDef && line.Trim().ToUpper() == "END") {
            return parser.props;  // Return results
          }

          // Attempt to parse the line
          if (!parser.tryParseNextLine(line)) throw new AuthParseException(path, lineNum);

        }

        // Read next line
        lineNum++;
        line = rd.ReadLine();

      }

      // If reached this point, we either encountered too many lines or the file
      // ended prematurely.
      throw new AuthParseException(path, -1);
    }

  }
}

internal class AuthParseException : Exception {

  private string path;
  private int lineNum;

  public AuthParseException(string path, int lineNum) {
    this.path = path;
    this.lineNum = lineNum;
  }

  public string Path {
    get { return path; }
  }
  public int LineNum {
    get { return lineNum; }
  }

  public override string Message {
    get {
      if (lineNum != -1) {
        return String.Format("Error parsing line {0} of {1}", lineNum, path);
      } else {
        return String.Format("Error parsing {1}", path);
      }
    }
  }

}
