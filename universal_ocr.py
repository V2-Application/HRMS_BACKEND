import base64
import json
import io
import os
from typing import List, Dict, Any, Optional
from openai import OpenAI
from PIL import Image
import fitz  # PyMuPDF for PDF processing
from docx import Document
import streamlit as st

class UniversalOCR:
    def __init__(self, api_key: str):
        """Initialize the OCR client with OpenAI API key."""
        self.client = OpenAI(api_key=api_key)
    
    def convert_pdf_to_images(self, pdf_path: str) -> List[Image.Image]:
        """Convert PDF pages to images."""
        images = []
        pdf_document = fitz.open(pdf_path)
        
        for page_num in range(len(pdf_document)):
            page = pdf_document.load_page(page_num)
            pix = page.get_pixmap(matrix=fitz.Matrix(2, 2))  # 2x zoom for better quality
            img_data = pix.tobytes("png")
            img = Image.open(io.BytesIO(img_data))
            images.append(img)
        
        pdf_document.close()
        return images
    
    def convert_docx_to_images(self, docx_path: str) -> List[Image.Image]:
        """Convert DOCX to images by taking screenshots of each page."""
        # For DOCX, we'll create a simple representation as text first
        # This is a simplified approach - in production, you might want to use 
        # libraries like python-docx2txt or convert to PDF first
        doc = Document(docx_path)
        
        # Create a text representation and convert to image
        text_content = []
        for paragraph in doc.paragraphs:
            if paragraph.text.strip():
                text_content.append(paragraph.text.strip())
        
        # Create a simple image with text content
        # This is a basic implementation - you might want to use a more sophisticated approach
        from PIL import Image, ImageDraw, ImageFont
        
        # Create a white image
        img = Image.new('RGB', (800, 1000), color='white')
        draw = ImageDraw.Draw(img)
        
        try:
            font = ImageFont.truetype("arial.ttf", 16)
        except:
            font = ImageFont.load_default()
        
        y_position = 50
        for text in text_content[:50]:  # Limit to first 50 paragraphs
            if y_position > 950:
                break
            draw.text((50, y_position), text, fill='black', font=font)
            y_position += 30
        
        return [img]
    
    def image_to_base64(self, image: Image.Image) -> str:
        """Convert PIL Image to base64 string."""
        buffer = io.BytesIO()
        image.save(buffer, format='PNG')
        img_bytes = buffer.getvalue()
        return base64.b64encode(img_bytes).decode('utf-8')
    
    def extract_text_from_image(self, image: Image.Image, columns_to_extract: List[str]) -> Dict[str, Any]:
        """Extract specified columns from an image using OpenAI Vision."""
        base64_image = self.image_to_base64(image)
        
        # Create the prompt for extraction
        columns_text = ", ".join(columns_to_extract)
        prompt = f"""Extract the following information from this document/image: {columns_text}. 

Return the result as a JSON object with the exact column names as keys. 

For education fields, if multiple entries exist, return as an array of strings.
For other fields with multiple values, return as an array.
If any information is not found, use null for that field.

Example format:
{{
    "Name": "John Doe",
    "Email": "john@example.com",
    "Education": ["Bachelor of Science", "Master of Engineering"]
}}"""
        
        try:
            response = self.client.chat.completions.create(
                model="gpt-4-turbo",
                messages=[
                    {
                        "role": "user",
                        "content": [
                            {"type": "text", "text": prompt},
                            {
                                "type": "image_url",
                                "image_url": {
                                    "url": f"data:image/png;base64,{base64_image}"
                                }
                            }
                        ],
                    }
                ],
                max_tokens=1000,
            )
            
            result_text = response.choices[0].message.content
            
            try:
                # Clean the response text (remove markdown code blocks if present)
                cleaned_text = result_text.strip()
                if cleaned_text.startswith('```json'):
                    cleaned_text = cleaned_text[7:]  # Remove ```json
                if cleaned_text.endswith('```'):
                    cleaned_text = cleaned_text[:-3]  # Remove ```
                cleaned_text = cleaned_text.strip()
                
                extracted_data = json.loads(cleaned_text)
                return extracted_data
            except json.JSONDecodeError:
                return {"error": "Failed to parse JSON response", "raw_response": result_text}
                
        except Exception as e:
            return {"error": f"API call failed: {str(e)}"}
    
    def process_file(self, file_path: str, columns_to_extract: List[str]) -> Dict[str, Any]:
        """Process a file (image, PDF, or DOCX) and extract specified columns."""
        file_extension = os.path.splitext(file_path)[1].lower()
        images = []
        
        if file_extension in ['.jpg', '.jpeg', '.png', '.bmp', '.tiff', '.gif']:
            # Direct image file
            image = Image.open(file_path)
            images = [image]
            
        elif file_extension == '.pdf':
            # Convert PDF to images
            images = self.convert_pdf_to_images(file_path)
            
        elif file_extension == '.docx':
            # Convert DOCX to images
            images = self.convert_docx_to_images(file_path)
            
        else:
            return {"error": f"Unsupported file format: {file_extension}"}
        
        # Process each page/image
        results = []
        for i, image in enumerate(images):
            page_result = self.extract_text_from_image(image, columns_to_extract)
            page_result["page_number"] = i + 1
            results.append(page_result)
        
        return {
            "file_path": file_path,
            "total_pages": len(images),
            "extracted_data": results
        }

def extract_data_from_file(file_path: str, columns: str, api_key: str) -> Dict[str, Any]:
    """
    Main function to extract data from a file.
    
    Args:
        file_path: Path to the file to process
        columns: Comma-separated string of columns to extract
        api_key: OpenAI API key
    
    Returns:
        Dictionary containing extracted data
    """
    # Parse columns
    columns_list = [col.strip() for col in columns.split(',')]
    
    # Initialize OCR
    ocr = UniversalOCR(api_key)
    
    # Process file
    result = ocr.process_file(file_path, columns_list)
    
    return result

if __name__ == "__main__":
    # Example usage
    api_key = os.environ.get("OPENAI_API_KEY", "")
    
    # Test with an image file
    result = extract_data_from_file(
        file_path="./aadhar/3.jpeg",
        columns="Name, Gender, Date of Birth, Aadhaar Number",
        api_key=api_key
    )
    
    print(json.dumps(result, indent=2))
