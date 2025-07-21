// Print functionality
function printReceipt(elementId) {
    const printContent = document.getElementById(elementId).innerHTML;
    const originalContent = document.body.innerHTML;

    document.body.innerHTML = printContent;
    window.print();
    document.body.innerHTML = originalContent;
    window.location.reload();
}

// PDF Download functionality
// PDF Download functionality
function downloadFile(filename, base64Data) {
    const link = document.createElement('a');
    link.href = 'data:application/pdf;base64,' + base64Data;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}