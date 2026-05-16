function auditLoggingExportToExcel(fileName, fileData) {
    var byteArray = new Uint8Array(fileData.length);
    for (var i = 0; i < fileData.length; i++) {
        byteArray[i] = fileData[i];
    }
    var blob = new Blob([byteArray], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    });

    // Create download link
    var url = window.URL.createObjectURL(blob);
    var link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();

    // Clean up
    setTimeout(function () {
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
    }, 100);
}
