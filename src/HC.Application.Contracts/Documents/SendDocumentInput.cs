using System;
using System.Collections.Generic;

namespace HC.Documents;

public  class SendDocumentInput
{
    public Guid DocumentId { get; set; }
    public List<Guid>? Recipients { get; set; }
    public List<Guid>? Departments { get; set; } 
}