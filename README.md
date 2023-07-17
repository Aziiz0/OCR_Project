# OCR_Project

Welcome to OCR Project repository! This project is designed to OCR files and return a dataset of information on the file.

## Prerequisites

Before getting started with this project, please ensure that you have the following software and dependencies installed:

1. **Visual Studio Code**:  
   [Download Visual Studio Code](https://code.visualstudio.com/download) and install it on your system.

2. **.NET**:  
   [Download .NET](https://dotnet.microsoft.com/download) and install it on your system.

3. **Xpdf Command-Line Tools**:  
   - Visit [https://www.xpdfreader.com/download.html](https://www.xpdfreader.com/download.html)
   - Download the appropriate version for your operating system.
   - Add the `bin64` directory to your system's PATH environment variable. This will allow you to access the Xpdf command-line tools from anywhere in your terminal.

4. **Adding Extra Fonts for Xpdf**
   - Visit [http://www.glyphandcog.com/support/q0016.html](http://www.glyphandcog.com/support/q0016.html)
   - Download the 2 fonts
   - Intructions will guide you but simply create an xpdfrc file with no extentions in your `bin64` directory.
   - Inside the xpdfrc file put:

     ```text
     fontFile Symbol       "/full/path/to/s050000l.pfb"
     fontFile ZapfDingbats "/full/path/to/d050000l.pfb"
     ```
     
     where `/full/path/to/` is the path to the downloaded fonts.

4. **Tesseract OCR Data**:  
   - Clone or download the [tessdata](https://github.com/tesseract-ocr/tessdata) repository.
   - Place the downloaded tessdata folder inside the same directory as your project.

## Installation

To use this project/program, follow the steps below:

1. Open your terminal or command prompt.

2. Run the following commands:

   ```shell
   dotnet add package Tesseract
   dotnet add package Pdf2Png
   ```

## Running the Project

To run the project, execute the following command in your terminal or command prompt:

```shell
dotnet run
```

That's it! The project will now be executed, and you can see the output in your console.

Feel free to explore the project's source code and make any necessary modifications to suit your needs.

If you encounter any issues or have any questions, please don't hesitate to reach out.

Happy coding!
