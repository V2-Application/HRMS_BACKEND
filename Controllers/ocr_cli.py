#!/usr/bin/env python3
"""
Command-line interface for Universal OCR
Usage: python ocr_cli.py <file_path> <columns> [output_file]
"""

import sys
import json
import os
from universal_ocr import extract_data_from_file

def main():
    if len(sys.argv) < 3:
        print("Usage: python ocr_cli.py <file_path> <columns> [output_file]")
        print("Example: python ocr_cli.py document.pdf 'Name,Email,Phone' result.json")
        sys.exit(1)
    
    file_path = sys.argv[1]
    columns = sys.argv[2]
    output_file = sys.argv[3] if len(sys.argv) > 3 else None
    
    # API Key from environment variable
    api_key = os.environ.get("OPENAI_API_KEY", "")
    
    try:
        # Check if file exists
        if not os.path.exists(file_path):
            result = {"error": f"File not found: {file_path}"}
        else:
            # Extract data
            result = extract_data_from_file(file_path, columns, api_key)
        
        # Output result
        json_output = json.dumps(result, indent=2)
        
        if output_file:
            with open(output_file, 'w', encoding='utf-8') as f:
                f.write(json_output)
            print(f"Results saved to: {output_file}")
        else:
            print(json_output)
            
    except Exception as e:
        error_result = {"error": str(e)}
        print(json.dumps(error_result, indent=2))
        sys.exit(1)

if __name__ == "__main__":
    main()
