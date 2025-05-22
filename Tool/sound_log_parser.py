import sys
import argparse
import os

def generate_sound_enum(input_text):
    # Remove BOM if present
    if input_text.startswith('\ufeff'):
        input_text = input_text[1:]
        
    lines = input_text.strip().split('\n')
    
    stripped_lines = [line.replace("[Client] ", "") for line in lines]
    
    pairs = []
    for i in range(0, len(stripped_lines), 2):
        if i + 1 < len(stripped_lines):
            sound_key = stripped_lines[i]
            hash_value = stripped_lines[i + 1]
            pairs.append((sound_key, hash_value))
    
    enum_text = "using System.ComponentModel;\n\npublic enum Sounds : uint\n{\n"
    
    for sound_key, hash_value in pairs:
        enum_key = sound_key.replace(".", "")
        
        # If the key starts with "Player", remove "Player" from the enum key name
        if enum_key.startswith("Player"):
            enum_key = enum_key[6:]  # Remove the first 6 characters ("Player")
        
        enum_text += f'    [Description("{sound_key}")]\n'
        enum_text += f'    {enum_key} = {hash_value},\n'
    
    enum_text += "}"
    
    return enum_text

def generate_sound_enum_from_file(file_path):
    with open(file_path, 'r', encoding='utf-8', errors='ignore') as file:
        input_text = file.read()
    return generate_sound_enum(input_text)

def main():
    parser = argparse.ArgumentParser(description='Generate Sounds enum from sound keys and hash values')
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
            with open(args.input, 'r', encoding='utf-8', errors='ignore') as file:
                input_text = file.read()
            if args.verbose:
                print(f"Successfully read input file with UTF-8 encoding")
        except FileNotFoundError:
            print(f"Error: Input file '{args.input}' not found.", file=sys.stderr)
            sys.exit(1)
        except Exception as e:
            print(f"Error: {e}", file=sys.stderr)
            sys.exit(1)
    else:
        if args.verbose:
            print("Reading from standard input")
        input_text = sys.stdin.read()
    
    output_content = generate_sound_enum(input_text)
    
    if args.output:
        base_name = os.path.basename(args.output)
        name_without_ext = os.path.splitext(base_name)[0]
        if name_without_ext:
            enum_name = name_without_ext
            output_content = output_content.replace('public enum Sounds', f'public enum {enum_name}')
            if args.verbose:
                print(f"Using enum name from output file: {enum_name}")
    
    if args.output:
        if args.verbose:
            print(f"Writing enum to output file: {args.output} with UTF-8 encoding")
        try:
            with open(args.output, 'w', encoding='utf-8') as file:
                file.write(output_content)
            print(f"Enum written to '{args.output}'.")
        except Exception as e:
            print(f"Error: Failed to write to output file: {e}", file=sys.stderr)
            sys.exit(1)
    else:
        if args.verbose:
            print("Writing enum to standard output:")
        print(output_content)

if __name__ == "__main__":
    main()