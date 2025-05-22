import re
import sys
import argparse
import os

def extract_root_strings(text):
    # Updated pattern to match complete paths with multiple dots
    pattern = r'(\w+(?:\.\w+)+)\s*='
    matches = re.findall(pattern, text)
    return matches

def extract_root_strings_from_file(file_path):
    with open(file_path, 'r') as file:
        content = file.read()
    return extract_root_strings(content)

def generate_csharp_class(root_strings, class_name="GameSounds"):
    cs_code = f"public static class {class_name} {{\n"
    cs_code += f"    private static readonly List<string> _soundPaths;\n"
    cs_code += f"    \n"
    cs_code += f"    static {class_name}() {{\n"
    cs_code += f"        _soundPaths = new List<string>\n"
    cs_code += f"        {{\n"
    
    for root in root_strings:
        cs_code += f"            \"{root}\",\n"
    
    cs_code += f"        }};\n"
    cs_code += f"    }}\n"
    cs_code += f"    \n"
    cs_code += f"    public static IReadOnlyList<string> SoundPaths => _soundPaths;\n"
    cs_code += f"}}"
    
    return cs_code

def main():
    parser = argparse.ArgumentParser(description='Extract root strings from text and generate C# class')
    parser.add_argument('-i', '--input', help='Path to input file')
    parser.add_argument('-o', '--output', help='Path to output file')
    parser.add_argument('-v', '--verbose', action='store_true', help='Enable verbose output')
    args = parser.parse_args()
    
    if args.verbose:
        print("Running in verbose mode")
    
    if args.input:
        if args.verbose:
            print(f"Reading from input file: {args.input}")
        try:
            root_strings = extract_root_strings_from_file(args.input)
            if args.verbose:
                print(f"Successfully read input file")
        except FileNotFoundError:
            print(f"Error: Input file '{args.input}' not found.", file=sys.stderr)
            sys.exit(1)
        except Exception as e:
            print(f"Error: {e}", file=sys.stderr)
            sys.exit(1)
    else:
        if args.verbose:
            print("Reading from standard input")
        text = sys.stdin.read()
        root_strings = extract_root_strings(text)
    
    if args.verbose:
        print(f"Found {len(root_strings)} root strings")
        for i, root in enumerate(root_strings, 1):
            print(f"  {i}: {root}")
    
    # Determine class name from output file if provided
    class_name = "GameSounds"  # Default
    if args.output:
        # Extract the filename without extension
        base_name = os.path.basename(args.output)
        name_without_ext = os.path.splitext(base_name)[0]
        if name_without_ext:
            # Convert to PascalCase if needed
            class_name = name_without_ext[0].upper() + name_without_ext[1:]
    
    output_content = generate_csharp_class(root_strings, class_name)
    if args.verbose:
        print(f"Generated C# class with name: {class_name}")
    
    if args.output:
        if args.verbose:
            print(f"Writing C# class to output file: {args.output}")
        try:
            with open(args.output, 'w') as file:
                file.write(output_content)
            print(f"C# class written to '{args.output}'.")
        except Exception as e:
            print(f"Error: Failed to write to output file: {e}", file=sys.stderr)
            sys.exit(1)
    else:
        if args.verbose:
            print("Writing C# class to standard output:")
        print(output_content)

if __name__ == "__main__":
    main()