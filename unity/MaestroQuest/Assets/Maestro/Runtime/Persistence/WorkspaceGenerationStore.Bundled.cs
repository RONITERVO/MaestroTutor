// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading;
namespace Maestro.Quest.Persistence
{
    internal sealed partial class WorkspaceGenerationStore
    {
        // Bundled content is an ordinary verified preview. Stream its archive through
        // one private file rather than allocating a second archive-sized memory buffer.
        internal PreparedWorkspaceGeneration PrepareSnapshot(WorkspaceArchiveSnapshot snapshot,CancellationToken cancellation=default)
        {
            if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            WorkspaceFileInventory.Parents(appRoot);
            using var lease=Lease(initialize:false);
            cancellation.ThrowIfCancellationRequested();
            string path=Path.Combine(staging,"bundled-"+Guid.NewGuid().ToString("N")+".zip");bool created=false;
            try {
                using var archive=new FileStream(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
                created=true;WorkspaceArchive.Write(archive,snapshot,cancellation);archive.Flush(true);
                cancellation.ThrowIfCancellationRequested();archive.Position=0;
                return PrepareArchiveLocked(archive,cancellation);
            }finally {
                if(created&&File.Exists(path)){WorkspaceArchive.NoLink(path);File.Delete(path);}
            }
        }
    }
}
