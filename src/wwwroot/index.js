document.addEventListener('DOMContentLoaded', () => {
    const rcodes = {
        0: 'NOERROR',
        1: 'FORMERR',
        2: 'SERVFAIL',
        3: 'NXDOMAIN',
        4: 'NOTIMP',
        5: 'REFUSED',
    };
    const phrases = {
        200: 'OK',
        201: 'Created',
        202: 'Accepted',
        204: 'No Content',
        206: 'Partial Content',
        301: 'Moved Permanently',
        302: 'Found',
        303: 'See Other',
        304: 'Not Modified',
        307: 'Temporary Redirect',
        308: 'Permanent Redirect',
        400: 'Bad Request',
        401: 'Unauthorized',
        403: 'Forbidden',
        404: 'Not Found',
        405: 'Method Not Allowed',
        406: 'Not Acceptable',
        408: 'Request Timeout',
        409: 'Conflict',
        410: 'Gone',
        411: 'Length Required',
        412: 'Precondition Failed',
        413: 'Payload Too Large',
        414: 'URI Too Long',
        415: 'Unsupported Media Type',
        416: 'Range Not Satisfiable',
        426: 'Upgrade Required',
        429: 'Too Many Requests',
        431: 'Request Header Fields Too Large',
        451: 'Unavailable For Legal Reasons',
        499: 'Client Closed',
        500: 'Internal Server Error',
        501: 'Not Implemented',
        502: 'Bad Gateway',
        503: 'Service Unavailable',
        504: 'Gateway Timeout',
    };
    const opcodes = {
        0x0: '', // Continuation
        0x1: 'Text',
        0x2: 'Binary',
        0x8: 'Close',
        0x9: 'Ping',
        0xA: 'Pong',
    };
    const network = document.forms.namedItem('network');
    const table = network.querySelector('table');
    const aside = network.querySelector('aside');
    const icase = new Intl.Collator('en', { sensitivity: 'base' });
    const number = new Intl.NumberFormat('en');
    const datetime = new Intl.DateTimeFormat('en',
        { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' });
    const readable = /^text\/|[/+](?:javascript|json|xml|vnd\.apple\.mpegurl)(?:$|;)/i;
    const drawable = /^image\//;
    /**
     * @param {Blob} blob
     * @returns {Promise<HTMLImageElement>}
     */
    const blob2img = blob => new Promise((resolve, reject) => {
        const img = new Image;
        img.addEventListener('load', () => {
            URL.revokeObjectURL(img.src);
            resolve(img);
        });
        img.addEventListener('error', e => {
            URL.revokeObjectURL(img.src);
            reject(e.error);
        });
        img.src = URL.createObjectURL(blob);
    });
    /**
     * @param {string} text
     * @returns {string}
     */
    const prettify = text => {
        if (/^\s*(?:\{.*\}|\[.*\])\s*$/s.test(text)) try {
            return JSON.stringify(JSON.parse(text), null, 2);
        } catch {
            return text;
        }
        if (/^\s*<(?!!DOCTYPE|html).*>\s*$/is.test(text)) {
            const doc = new DOMParser().parseFromString(text, 'text/xml');
            if (doc.querySelector('parsererror'))
                return text;
            const xslt = new XSLTProcessor();
            xslt.importStylesheet(new DOMParser().parseFromString(`
                <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                    <xsl:output indent="yes"/>
                    <xsl:template match="node()|@*">
                        <xsl:copy>
                            <xsl:apply-templates select="node()|@*"/>
                        </xsl:copy>
                    </xsl:template>
                </xsl:stylesheet>
            `, 'application/xml'));
            return new XMLSerializer().serializeToString(xslt.transformToDocument(doc));
        }
        return text;
    }
    /**
     * @param {ArrayBuffer} buffer
     * @returns {string}
     */
    const stringify = buffer => String.fromCharCode(...new Uint8Array(buffer, 0, Math.min(buffer.byteLength, 8192)))
                              + (buffer.byteLength > 8192 ? '...' : '');
    /**
     * @type {Map<string, {
     *     time: number;
     *     elapsed: number;
     *     protocol: string;
     *     request: { method: string, uri: string, headers: string, body?: string };
     *     response: { status: number, headers: string, body?: string | Node };
     *     messages: { code: number, size: number, time: number }[];
     * }>}
     */
    const records = new Map;

    /**
     * @param {boolean} closable
     */
    async function help(closable = true) {
        const dialog = document.querySelector('dialog');
        const resolv = await (await fetch('resolv.conf')).text();
        const nameservers = [...resolv.matchAll(/^nameserver (.+)$/gm)].map(([_, a]) => a);
        dialog.querySelector('form').elements.namedItem('close').disabled = !closable;
        dialog.querySelector('.nameservers').textContent = nameservers.join(', ');
        dialog.show();
    }
    function render() {
        const record = records.get(aside.dataset.id);
        if (!record)
            return;
        const frag = document.createDocumentFragment();
        frag.appendChild(document.createTextNode(record.request.headers));
        if (record.request.body?.length) {
            frag.appendChild(document.createTextNode('\n\n'));
            const source = record.request.body, pretty = prettify(source);
            if (pretty !== source) {
                const code = frag.appendChild(document.createElement('code'));
                code.textContent = source;
                code.addEventListener('click', () => code.textContent = code.textContent === source ? pretty : source);
            } else {
                frag.appendChild(document.createTextNode(record.request.body));
            }
        }
        frag.appendChild(document.createTextNode('\n\n'));
        frag.appendChild(document.createTextNode(record.response.headers));
        if (record.response.body instanceof Node) {
            frag.appendChild(document.createTextNode('\n\n'));
            frag.appendChild(record.response.body);
        } else if (record.response.body?.length) {
            frag.appendChild(document.createTextNode('\n\n'));
            const source = record.response.body, pretty = prettify(source);
            if (pretty !== source) {
                const code = frag.appendChild(document.createElement('code'));
                code.textContent = source;
                code.addEventListener('click', () => code.textContent = code.textContent === source ? pretty : source);
            } else {
                frag.appendChild(document.createTextNode(record.response.body));
            }
        }
        if (record.messages.length) {
            frag.appendChild(document.createTextNode('\n'));
            /** @type {[mode: string, code: string, size: string, time: string][]} */
            const rows = record.messages.map(({ code, size, time }) =>
                [size < 0 ? '⬆' : '⬇', opcodes[code] ?? `(${code})`, number.format(Math.abs(size)), datetime.format(time)]);
            const max = rows.reduce((max, [_, code, size]) => {
                return {
                    code: Math.max(max.code, code.length),
                    size: Math.max(max.size, size.length),
                };
            }, { code: 0, size: 0 });
            for (const [mode, code, size, time] of rows) {
                frag.appendChild(document.createTextNode('\n'));
                frag.appendChild(document.createTextNode(`${mode} ${code.padEnd(max.code)} ${size.padStart(max.size)} B ${time}`));
            }
        }
        aside.querySelector('pre').replaceChildren(frag);
    }

    network.addEventListener('reset', e => {
        e.preventDefault();
        table.tBodies[0].replaceChildren();
        delete aside.dataset.id;
        records.clear();
    });
    network.elements.namedItem('download').addEventListener('click', async e => {
        e.preventDefault();
        if (!window.showSaveFilePicker)
            return alert('Not supported in this platform.');
        const handle = await showSaveFilePicker({
            suggestedName: `${new URL([...records.values()].find(e => /^HTTP/i.test(e.protocol))?.request?.uri ?? location).hostname}.har`,
            types: [{ accept: { 'application/json': ['.har'] }, description: 'HTTP Archive (HAR) file' }],
        }).catch(ex => {
            if (ex instanceof DOMException && ex.name === 'NotAllowedError') {
                const scheme = navigator.userAgentData.brands.some(x => x.brand === 'Microsoft Edge') ? 'edge' : 'chrome';
                alert(`${scheme}://settings/content/fileEditing is not allowed.`);
            }
        });
        if (!handle)
            return;
        const stream = await handle.createWritable();
        const write = async (...lines) => await stream.write(lines.filter(line => !!line).join('\n') + '\n');
        try {
            await write('{',
                        '  "log": {',
                        '    "version": "1.2",',
                        '    "creator": {',
                        '      "name": "Triode",',
                        '      "version": "0.0.0"',
                        '    },',
                        '    "entries": [');
            const entries = [...records].filter(([, e]) => /^HTTP/i.test(e.protocol));
            for (const [id, e] of entries) {
                await write(`      {`,
                            `        "startedDateTime": ${JSON.stringify(new Date(e.time))},`,
                            `        "time": ${e.elapsed},`,
                            `        "request": {`,
                            `          "method": "${e.request.method}",`,
                            `          "url": ${JSON.stringify(e.request.uri)},`,
                            `          "httpVersion": "${e.protocol}",`,
                            `          "cookies": [],`,
                            `          "headers": [`,
                            e.request.headers.split('\n').slice(1).map(p => p.split(': ', 2)).map(([name, value]) => [
                            `            {`,
                            `              "name": ${JSON.stringify(name)},`,
                            `              "value": ${JSON.stringify(value)}`,
                            `            }`,
                            ].join('\n')).join(',\n'),
                            `          ],`,
                            `          "queryString": [],`);
                if (!/^(?:HEAD|GET)$/i.test(e.request.method)) {
                    const q = await fetch(`?q=${id}`);
                    if (q.ok) {
                        const postData = new Uint8Array(await q.arrayBuffer());
                        await write(`          "postData": {`,
                                    `            "mimeType": ${JSON.stringify(q.headers.get('content-type') ?? '')},`,
                                    `            "text": ${JSON.stringify(String.fromCharCode(...postData))}`,
                                    `          },`);
                    }
                }
                await write(`          "headersSize": -1,`,
                            `          "bodySize": -1`,
                            `        },`,
                            `        "response": {`,
                            `          "status": ${e.response.status},`,
                            `          "statusText": "${phrases[e.response.status] ?? ''}",`,
                            `          "httpVersion": "${e.protocol}",`,
                            `          "cookies": [],`,
                            `          "headers": [`,
                            e.response.headers.split('\n').slice(1).map(p => p.split(': ', 2)).map(([name, value]) => [
                            `            {`,
                            `              "name": ${JSON.stringify(name)},`,
                            `              "value": ${JSON.stringify(value)}`,
                            `            }`,
                            ].join('\n')).join(',\n'),
                            `          ],`,
                            `          "content": {`);
                if (/^HEAD$/i.test(e.request.method) || e.response.status < 200 || [204, 304].includes(e.response.status)) {
                    await write(`            "size": 0,`,
                                `            "mimeType": ""`);
                } else {
                    const r = await fetch(`?r=${id}`);
                    if (r.ok) {
                        const content = new Uint8Array(await r.arrayBuffer());
                        try {
                            const text = btoa(String.fromCharCode(...content));
                            await write(`            "size": ${content.length},`,
                                        `            "mimeType": ${JSON.stringify(r.headers.get('content-type') ?? '')},`,
                                        `            "text": "${text}",`,
                                        `            "encoding": "base64"`);
                        } catch {
                            await write(`            "size": ${content.length},`,
                                        `            "mimeType": ${JSON.stringify(r.headers.get('content-type') ?? '')}`);
                        }
                    } else {
                        await write(`            "size": 0,`,
                                    `            "mimeType": ""`);
                    }
                }
                await write(`          },`,
                            `          "redirectURL": "",`,
                            `          "headersSize": -1,`,
                            `          "bodySize": -1`,
                            `        },`,
                            `        "cache": {},`,
                            `        "timings": {`,
                            `          "send": -1,`,
                            `          "wait": -1,`,
                            `          "receive": -1`,
                            `        }`,
                            `      }${id === entries.at(-1)[0] ? '' : ','}`);
            }
            await write('    ]',
                        '  }',
                        '}');
        } finally {
            await stream.close();
        }
    })
    network.elements.namedItem('help').addEventListener('click', e => {
        e.preventDefault();
        void help();
    });
    network.elements.namedItem('restore').addEventListener('click', e => {
        e.preventDefault();
        delete aside.dataset.id;
    });

    if (location.protocol !== 'https:') {
        const favicon = new Image;
        favicon.addEventListener('load', () => location.protocol = 'https:');
        favicon.addEventListener('error', () => help(false));
        favicon.src = `https://${location.host}/favicon.png`;
        return;
    }

    const socket = new WebSocket(location.href.replace(/^http/, 'ws'));
    socket.addEventListener('message', e => {
        const bottom = table.scrollTop >= table.scrollHeight - table.getBoundingClientRect().height;
        /** @type {[string, number, string, string, string, string, number, number, number, string[][], string[][]]} */
        const [id, time, from, method, uri, protocol, status, size, elapsed, reqhdrs, reshdrs] = JSON.parse(e.data);
        if (!uri) {
            records.get(id)?.messages?.push({ code: status, size, time });
            if (id === aside.dataset.id)
                render();
            return;
        }
        records.set(id, {
            time,
            elapsed,
            protocol,
            request: {
                method,
                uri,
                headers: reqhdrs.reduce((s, [k, v]) => `${s}\n${k}: ${v}`, `${method} ${uri} ${protocol}`),
            },
            response: {
                status,
                headers: reshdrs.reduce((s, [k, v]) => `${s}\n${k}: ${v}`, `${protocol} ${status} ${rcodes[status] ?? phrases[status] ?? ''}`),
            },
            messages: [],
        });
        const tr = document.createElement('tr');
        tr.insertCell().textContent = datetime.format(time);
        tr.insertCell().textContent = from;
        tr.insertCell().textContent = method;
        tr.insertCell().textContent = uri;
        tr.insertCell().textContent = status;
        tr.insertCell().textContent = number.format(size);
        tr.insertCell().textContent = number.format(elapsed);
        tr.insertCell().textContent = reqhdrs.find(([name]) => !icase.compare(name, 'Referer'))?.[1];
        tr.insertCell().textContent = reqhdrs.find(([name]) => !icase.compare(name, 'User-Agent'))?.[1];
        if (status === null || 0 < status && status < 100 || 400 <= status)
            tr.classList.add('error');
        table.tBodies[0].appendChild(tr);
        tr.addEventListener('click', () => {
            aside.dataset.id = id;
            render();
            const record = records.get(id);
            console.assert(record);
            const hasreq = !/^(?:A|AAAA|HEAD|GET)$/i.test(method);
            const hasres = !/^(?:A|AAAA|HEAD)$/i.test(method) && !/^wss?:/.test(uri) && 200 <= status && ![204, 304].includes(status);
            (hasreq || hasres) && Promise.all([
                hasreq ? fetch(`?q=${id}`) : Promise.resolve(),
                hasres ? fetch(`?r=${id}`) : Promise.resolve(),
            ]).then(async ([q, r]) => {
                record.request.body = !+q?.headers?.get('content-length') ? ''
                    : readable.test(q.headers.get('content-type')) ? await q.text()
                    : stringify(await q.arrayBuffer());
                record.response.body = !+r?.headers?.get('content-length') ? ''
                    : drawable.test(r.headers.get('content-type')) ? await blob2img(await r.blob())
                    : readable.test(r.headers.get('content-type')) ? await r.text()
                    : stringify(await r.arrayBuffer());
                render();
            });
        });
        if (bottom)
            table.scrollTop = table.scrollHeight;
    });

    document.addEventListener('keydown', e => {
        const modifiers = ['Control', 'Alt', 'Shift', 'Meta'], key = /^[a-z]$/.test(e.key) ? e.key.toUpperCase() : e.key;
        if (modifiers.includes(key))
            return;
        const combination = [...modifiers.filter(m => e.getModifierState(m)), key].join('+');
        switch (combination) {
        case 'Escape':
            delete aside.dataset.id;
            break;
        }
    });
});
