interface FilePickerOptions {
    excludeAcceptAllOption?: boolean;
    types?: { description?: string, accept: Record<string, string> }[];
}

interface FileSystemCreateWritableOptions {
    keepExistingData?: boolean;
}

interface FileSystemFileHandle extends FileSystemHandle {
    createWritable(options?: FileSystemCreateWritableOptions): Promise<FileSystemWritableFileStream>;
    getFile(): Promise<File>;
}

interface FileSystemHandle {
    readonly kind: "file" | "directory";
    readonly name: string;
    isSameEntry(fileSystemHandle: FileSystemHandle): boolean;
}

interface FileSystemWritableFileStream extends WritableStream<BlobPart> {
    seek(position: number): Promise<undefined>;
    truncate(size: number): Promise<undefined>;
    write(data: BlobPart): Promise<undefined>;
}

declare function showOpenFilePicker(options?: FilePickerOptions & { multiple?: boolean }): Promise<FileSystemFileHandle[]>;
declare function showSaveFilePicker(options?: FilePickerOptions & { suggestedName?: string }): Promise<FileSystemFileHandle>;
