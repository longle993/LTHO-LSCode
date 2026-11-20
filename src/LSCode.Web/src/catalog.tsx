import { readClipboard, writeClipboard } from "./bridge";
import { useState, useEffect } from "react";
import { Button, ConfirmDialog, Select, DataGrid, IconButton, WorkbenchNotice } from "./ui";
import { Plus, Trash2 } from "lucide-react";
import { request } from "./bridge";
import type { Target, Table } from "./bridge";
import { invalidateTableCatalog } from "./features/TableTool";
type Field = {
  originalName?: string;
  name: string;
  type: string;
  size: string;
  required: boolean;
  key: boolean;
  unique: boolean;
  identity: boolean;
  default: string | null;
  identitySeed: string;
  identityIncrement: string;
  collation: string | null;
  reference: string[] | null;
  keyOrder: number;
  nullDefault: boolean;
};
type Column = {
  name: string;
  type: string;
  nullable: boolean;
  identity: boolean;
  computed: boolean;
  generated: number;
  managed: boolean;
  hasDefault: boolean;
};
const types = [
  "nvarchar",
  "varchar",
  "nchar",
  "char",
  "int",
  "bigint",
  "smallint",
  "tinyint",
  "decimal",
  "numeric",
  "bit",
  "date",
  "datetime",
  "datetime2",
  "uniqueidentifier",
];
const blank = (): Field => ({
  name: "",
  type: "nvarchar",
  size: "128",
  required: false,
  key: false,
  unique: false,
  identity: false,
  default: null,
  identitySeed: "1",
  identityIncrement: "1",
  collation: null,
  reference: null,
  keyOrder: 0,
  nullDefault: false,
});
let clipboardPacket:
  | {
      text: string;
      fields?: Field[];
      columns?: string[];
      rows?: (string | null)[][];
    }
  | undefined;
const literal = (value: string) =>
  value.startsWith(":") ? ":text:" + value : value;
const decode = (value: string) =>
  value.startsWith(":text:") ? value.slice(6) : value;
export function CatalogTool({
  target,
  onDirtyChange,
  onBusyChange,
}: {
  target?: Target;
  onDirtyChange?: (dirty: boolean) => void;
  onBusyChange?: (busy: boolean) => void;
}) {
  const [schema, setSchema] = useState("dbo"),
    [table, setTable] = useState(""),
    [creating, setCreating] = useState(true),
    [fields, setFields] = useState<Field[]>([]),
    [columns, setColumns] = useState<Column[]>([]),
    [data, setData] = useState<Table>(),
    [rows, setRows] = useState<(string | null)[][]>([]),
    [signature, setSignature] = useState(""),
    [page, setPage] = useState(0);
  const [selectedRows, setSelectedRows] = useState<number[]>([]);
  const [mode, setMode] = useState("structure"),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [preview, setPreview] = useState<{ previewId: string; sql: string }>(),
    [notice, setNotice] = useState("");
  const [advanced, setAdvanced] = useState(-1),
    [references, setReferences] = useState<Table>(),
    [source, setSource] = useState<{ schema: string; table: string }>();
  const [confirmation, setConfirmation] = useState<{ message: string; action: () => void }>();
  const dirty = fields.length > 0 || rows.length > 0;
  useEffect(() => {
    onDirtyChange?.(dirty);
  }, [dirty, onDirtyChange]);
  useEffect(() => {
    onBusyChange?.(busy);
  }, [busy, onBusyChange]);
  async function run(fn: () => Promise<void>) {
    setError("");
    setBusy(true);
    try {
      await fn();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  function guardDraft(message: string, action: () => void) {
    if (!dirty) action();
    else setConfirmation({ message, action });
  }
  function update(i: number, patch: Partial<Field>) {
    setFields((old) => old.map((f, k) => (i === k ? { ...f, ...patch } : f)));
    setPreview(undefined);
  }
  async function load(nextPage = 0) {
    const r = await request<{
      signature: string;
      columns: Column[];
      data: Table;
    }>("catalog.load", { targetId: target?.id, schema, table, page: nextPage });
    setCreating(false);
    setSignature(r.signature);
    setColumns(r.columns);
    setData(r.data);
    setFields([]);
    setRows([]);
    setPreview(undefined);
    setPage(nextPage);
    setSelectedRows([]);
  }
  async function createPreview() {
    const definitions = fields.map((f) => {
      if (
        !source ||
        !f.reference ||
        f.reference[0] !== source.schema ||
        f.reference[1] !== source.table
      )
        return f;
      const referenced = fields.find((x) => x.originalName === f.reference![2]);
      if (!referenced)
        throw new Error("Trường được tham chiếu đã bị bỏ khỏi bản nháp.");
      return {
        ...f,
        reference: [schema, table, referenced.name, referenced.type],
      };
    });
    setPreview(
      await request("catalog.preview", {
        targetId: target?.id,
        schema,
        table,
        creating,
        signature,
        fields: definitions,
        rows,
      }),
    );
    setMode("preview");
  }
  const managed = (c: Column) =>
    c.identity || c.computed || c.generated !== 0 || c.managed;
  async function copyRecords() {
    if (!data) return;
    const indexes = columns
      .map((c, i) => (managed(c) ? -1 : i))
      .filter((i) => i >= 0);
    const names = indexes.map((i) => columns[i].name);
    const values = selectedRows.map((i) => indexes.map((j) => data.rows[i][j]));
    if (values.length === 0)
      throw new Error("Chọn dòng bằng cột số thứ tự trước khi sao chép.");
    const text = await request<string>("catalog.clipboard.write", {
      targetId: target?.id,
      rows: [names, ...values],
    });
    clipboardPacket = {
      text,
      columns: names,
      rows: values.map((row) =>
        row.map((v) => (v === null ? ":null" : literal(v))),
      ),
    };
    await writeClipboard(text);
    setNotice(
      `Đã sao chép ${values.length} dòng nghiệp vụ, không gồm cột tự động.`,
    );
  }
  async function pasteRecords() {
    const text = await readClipboard();
    const editable = columns.filter((c) => !managed(c));
    let names = editable.map((c) => c.name),
      pasted: (string | null)[][];
    if (
      clipboardPacket?.text === text &&
      clipboardPacket.rows &&
      clipboardPacket.columns
    ) {
      pasted = clipboardPacket.rows;
      names = clipboardPacket.columns;
    } else {
      const raw = await request<string[][]>("catalog.clipboard.read", {
        targetId: target?.id,
        text,
      });
      const header = raw[0]?.map(decode);
      if (
        header?.length &&
        header.every((n) =>
          editable.some((c) => c.name.toLowerCase() === n.toLowerCase()),
        )
      ) {
        names = header;
        pasted = raw.slice(1);
      } else pasted = raw;
    }
    if (pasted.length + rows.length > 1000)
      throw new Error("Tối đa 1000 dòng nháp.");
    if (
      new Set(names.map((n) => n.toLowerCase())).size !== names.length ||
      names.some(
        (n) => !editable.some((c) => c.name.toLowerCase() === n.toLowerCase()),
      )
    )
      throw new Error("Tên cột clipboard không khớp các trường có thể nhập.");
    if (pasted.some((r) => r.length !== names.length))
      throw new Error(
        `Clipboard cần ${names.length} cột nghiệp vụ hoặc hàng đầu là tên cột.`,
      );
    setRows([
      ...rows,
      ...pasted.map((row) =>
        columns.map((c) =>
          managed(c)
            ? null
            : (row[
                names.findIndex((n) => n.toLowerCase() === c.name.toLowerCase())
              ] ?? null),
        ),
      ),
    ]);
    setPreview(undefined);
  }
  async function copyFields() {
    const values = fields.length
      ? fields
      : await request<Field[]>("catalog.duplicate", {
          targetId: target?.id,
          schema,
          table,
        });
    const text = await request<string>("catalog.clipboard.write", {
      targetId: target?.id,
      rows: [
        [
          "Name",
          "Type",
          "Size",
          "Required",
          "Key",
          "Unique",
          "Identity",
          "Default",
        ],
        ...values.map((f) => [
          f.name,
          f.type,
          f.size,
          f.required ? "1" : "0",
          f.key ? "1" : "0",
          f.unique ? "1" : "0",
          f.identity ? "1" : "0",
          f.default ?? "",
        ]),
      ],
    });
    clipboardPacket = { text, fields: structuredClone(values) };
    await writeClipboard(text);
    setNotice(`Đã sao chép ${values.length} trường.`);
  }
  async function pasteFields() {
    const text = await readClipboard();
    let values: Field[];
    if (clipboardPacket?.text === text && clipboardPacket.fields)
      values = structuredClone(clipboardPacket.fields);
    else {
      const raw = (
        await request<string[][]>("catalog.clipboard.read", {
          targetId: target?.id,
          text,
        })
      ).map((row) => row.map(decode));
      if (["name", "tên trường"].includes(raw[0]?.[0]?.toLowerCase()))
        raw.shift();
      if (raw.length > 1000 || raw.some((r) => r.length !== 8))
        throw new Error(
          "Dán cấu trúc cần 8 cột: Name, Type, Size, Required, Key, Unique, Identity, Default.",
        );
      const yes = (v: string) =>
        ["1", "true", "yes", "có"].includes(v.toLowerCase());
      values = raw.map((r) => ({
        ...blank(),
        name: r[0],
        type: r[1],
        size: r[2],
        required: yes(r[3]),
        key: yes(r[4]),
        unique: yes(r[5]),
        identity: yes(r[6]),
        default: r[7] || null,
      }));
    }
    if (
      values.some((f) =>
        ["user_id0", "user_id2", "datetime0", "datetime2"].includes(
          f.name.toLowerCase(),
        ),
      )
    )
      throw new Error("Không dán đè trường hệ thống.");
    setFields([...fields, ...values]);
    setPreview(undefined);
    setNotice("Đã dán vào bản nháp, chưa ghi database.");
  }
  return <>
    <fieldset className="tool-body large catalog-fieldset" disabled={busy}>
      <p className="muted">
        {target
          ? `${target.server} / ${target.database}`
          : "Chọn kết nối SQL trước khi thiết kế danh mục."}{" "}
        · {creating ? "Bảng mới" : "Bảng hiện có"}
        {dirty ? " · Có bản nháp chưa lưu" : ""}
      </p>
      <div className="tool-toolbar">
        <input
          aria-label="Schema"
          style={{ maxWidth: 120 }}
          value={schema}
          disabled={!creating || busy}
          onChange={(e) => setSchema(e.target.value)}
        />
        <input
          aria-label="Tên bảng"
          placeholder="Tên bảng"
          value={table}
          disabled={busy}
          onChange={(e) => {
            setTable(e.target.value);
            setPreview(undefined);
          }}
        />
        <Button
          disabled={!target || !table || busy}
          onClick={() => guardDraft("Bỏ bản nháp hiện tại và mở cấu trúc của bảng đã nhập?",
            () => void run(async () => { await load(); }))}
        >
          Mở bảng
        </Button>
        <Button
          disabled={busy}
          onClick={() => guardDraft("Bỏ bản nháp hiện tại và bắt đầu một bảng mới?", () => {
              setCreating(true);
              setFields([]);
              setRows([]);
              setColumns([]);
              setData(undefined);
              setSignature("");
              setPreview(undefined);
              setTable("");
              setSource(undefined);
          })}
        >
          Bảng mới
        </Button>
        <Button
          disabled={!target || !table || busy}
          onClick={() => guardDraft("Bỏ bản nháp hiện tại để nhân bản cấu trúc bảng đã nhập?", () =>
            void run(async () => {
              const f = await request<Field[]>("catalog.duplicate", {
                targetId: target?.id,
                schema,
                table,
              });
              setSource({ schema, table });
              setFields(f.map((x) => ({ ...x, originalName: x.name })));
              setCreating(true);
              setRows([]);
              setColumns([]);
              setData(undefined);
              setSignature("");
              setTable(table + "_copy");
              setPreview(undefined);
              setNotice("Đã tạo bản nháp cấu trúc, chưa ghi database.");
            }))}
        >
          Nhân bản cấu trúc
        </Button>
      </div>
      <div className="tool-toolbar">
        {[
          ["structure", "Cấu trúc"],
          ["data", "Dữ liệu"],
          ["preview", "SQL xem trước"],
        ].map(([id, label]) => (
          <Button
            key={id}
            variant={mode === id ? "secondary" : "ghost"}
            disabled={id === "data" && creating}
            onClick={() => setMode(id)}
          >
            {label}
          </Button>
        ))}
        <div className="toolbar-spacer" />
        <Button
          disabled={!target || !table || !dirty || busy}
          onClick={() => void run(createPreview)}
        >
          Xem trước
        </Button>
        <Button
          variant="primary"
          disabled={!preview || busy}
          onClick={() =>
            void run(async () => {
              if (!preview || !target) return;
              const token = preview.previewId;
              setPreview(undefined);
              await request("catalog.commit", {
                targetId: target.id,
                previewId: token,
              });
              invalidateTableCatalog(target.id);
              setNotice("Đã lưu.");
              await load();
            })
          }
        >
          Lưu vào database
        </Button>
      </div>
      {mode === "structure" && (
        <>
          <div className="system-fields">
            Bốn trường hệ thống: user_id0, user_id2 (int), datetime0, datetime2
            (datetime). Tự thêm khi tạo bảng mới.
          </div>
          {columns.length > 0 && (
            <DataGrid
              table={{
                columns: [
                  "Tên trường",
                  "Kiểu",
                  "Nullable",
                  "Tự tăng",
                  "Hệ thống",
                ],
                rows: columns.map((c) => [
                  c.name,
                  c.type,
                  String(c.nullable),
                  String(c.identity),
                  String(c.managed),
                ]),
              }}
            />
          )}
          <div className="tool-toolbar">
            <Button
              disabled={rows.length > 0 || (!fields.length && creating)}
              onClick={() => void run(copyFields)}
            >
              Sao chép cấu trúc
            </Button>
            <Button
              disabled={rows.length > 0}
              onClick={() => void run(pasteFields)}
            >
              Dán trường
            </Button>
          </div>
          <div className="catalog-grid">
            <table>
              <thead>
                <tr>
                  <th>Tên</th>
                  <th>Kiểu</th>
                  <th>Độ dài / p,s</th>
                  <th>Bắt buộc</th>
                  <th>PK</th>
                  <th>Unique</th>
                  <th>Identity</th>
                  <th>Default literal</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {fields.map((f, i) => (
                  <tr key={i}>
                    <td>
                      <input
                        aria-label={`Tên trường ${i + 1}`}
                        value={f.name}
                        onChange={(e) => update(i, { name: e.target.value })}
                      />
                    </td>
                    <td>
                      <Select
                        label="Kiểu"
                        value={f.type}
                        options={types.map((v) => ({ value: v, label: v }))}
                        onChange={(v) =>
                          update(i, {
                            type: v,
                            size:
                              v === "datetime2"
                                ? "7"
                                : v === "decimal" || v === "numeric"
                                  ? "18,2"
                                  : "128",
                          })
                        }
                      />
                    </td>
                    <td>
                      <input
                        aria-label="Độ dài"
                        value={f.size}
                        onChange={(e) => update(i, { size: e.target.value })}
                      />
                    </td>
                    {(["required", "key", "unique", "identity"] as const).map(
                      (key) => (
                        <td key={key}>
                          <input
                            aria-label={key}
                            type="checkbox"
                            checked={f[key]}
                            onChange={(e) =>
                              update(i, { [key]: e.target.checked })
                            }
                          />
                        </td>
                      ),
                    )}
                    <td>
                      <input
                        aria-label="Default literal"
                        value={f.default ?? ""}
                        onChange={(e) =>
                          update(i, {
                            default:
                              e.target.value === "" ? null : e.target.value,
                          })
                        }
                      />
                    </td>
                    <td>
                      <IconButton
                        label="Bỏ trường nháp"
                        onClick={() => {
                          setFields(fields.filter((_, k) => k !== i));
                          setPreview(undefined);
                        }}
                      >
                        <Trash2 size={14} />
                      </IconButton>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {advanced >= 0 && fields[advanced] && (
            <FieldAdvanced
              field={fields[advanced]}
              references={references}
              onChange={(patch) => update(advanced, patch)}
              onClose={() => setAdvanced(-1)}
            />
          )}
          <Button
            disabled={busy || rows.length > 0}
            onClick={() => {
              setFields([...fields, blank()]);
              setPreview(undefined);
            }}
          >
            <Plus size={14} />
            Thêm trường
          </Button>
        </>
      )}
      {mode === "data" && (
        <>
          <div className="tool-toolbar">
            <Button
              disabled={busy || fields.length > 0}
              onClick={() => {
                setRows([...rows, columns.map(() => null)]);
                setPreview(undefined);
              }}
            >
              Thêm bản ghi nháp
            </Button>
            <Button
              disabled={busy || fields.length > 0}
              onClick={() =>
                void run(async () => {
                  await pasteRecords();
                })
              }
            >
              Dán nhiều dòng
            </Button>
            <Button
              disabled={!data || !selectedRows.length}
              onClick={() =>
                void run(async () => {
                  await copyRecords();
                })
              }
            >
              Sao chép dòng chọn
            </Button>
            <Button
              disabled={!rows.length}
              onClick={() => {
                setRows([]);
                setPreview(undefined);
              }}
            >
              Bỏ bản ghi nháp
            </Button>
            <Button
              disabled={busy || page === 0 || dirty}
              onClick={() => void run(() => load(page - 1))}
            >
              Trang trước
            </Button>
            <span>Trang {page + 1}</span>
            <Button
              disabled={busy || dirty || !data || data.rows.length < 100}
              onClick={() => void run(() => load(page + 1))}
            >
              Trang sau
            </Button>
          </div>
          {rows.length > 0 ? (
            <div className="catalog-grid">
              <p className="muted">
                NULL: :null · DEFAULT: :default · Chuỗi rỗng: :empty · Chưa ghi
                cho tới khi Lưu.
              </p>
              <table>
                <thead>
                  <tr>
                    {columns.map((c) => (
                      <th key={c.name}>{c.name}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r, i) => (
                    <tr key={i}>
                      {r.map((v, j) => (
                        <td key={j}>
                          <input
                            aria-label={`Dòng ${i + 1} ${columns[j].name}`}
                            disabled={managed(columns[j])}
                            placeholder={
                              managed(columns[j]) ? "Tự động" : "DEFAULT"
                            }
                            value={v ?? ""}
                            onChange={(e) => {
                              setRows(
                                rows.map((row, k) =>
                                  k === i
                                    ? row.map((x, n) =>
                                        n === j ? e.target.value : x,
                                      )
                                    : row,
                                ),
                              );
                              setPreview(undefined);
                            }}
                          />
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            data && (
              <DataGrid
                table={data}
                selectedRows={selectedRows}
                onSelectionChange={setSelectedRows}
              />
            )
          )}
        </>
      )}
      {mode === "preview" && (
        <textarea
          className="tool-text"
          aria-label="SQL xem trước"
          readOnly
          value={
            preview?.sql ?? "Nhấn Xem trước để kiểm tra SQL trước khi lưu."
          }
        />
      )}
    </fieldset>
    <WorkbenchNotice error={error} status={notice} onClose={() => { setError(""); setNotice(""); }} />
    {confirmation && <ConfirmDialog title="Bỏ bản nháp?" message={confirmation.message} confirmLabel="Bỏ thay đổi" danger
      onClose={() => setConfirmation(undefined)} onConfirm={() => {
        const action = confirmation.action;
        setConfirmation(undefined);
        action();
      }} />}
  </>;
}
function FieldAdvanced({
  field,
  references,
  onChange,
  onClose,
}: {
  field: Field;
  references?: Table;
  onChange: (patch: Partial<Field>) => void;
  onClose: () => void;
}) {
  const options = (references?.rows ?? []).filter((r) => r[3] === field.type);
  return (
    <div className="form">
      <div className="tool-toolbar">
        <h3>Chi tiết trường {field.name || "chưa đặt tên"}</h3>
        <div className="toolbar-spacer" />
        <Button variant="ghost" onClick={onClose}>
          Đóng chi tiết
        </Button>
      </div>
      <div className="form-row">
        <label>
          Identity seed
          <input
            value={field.identitySeed}
            onChange={(e) => onChange({ identitySeed: e.target.value })}
          />
        </label>
        <label>
          Identity increment
          <input
            value={field.identityIncrement}
            onChange={(e) => onChange({ identityIncrement: e.target.value })}
          />
        </label>
        <label>
          Thứ tự khóa chính
          <input
            type="number"
            value={field.keyOrder}
            onChange={(e) => onChange({ keyOrder: Number(e.target.value) })}
          />
        </label>
        <label>
          Collation
          <input
            value={field.collation ?? ""}
            onChange={(e) => onChange({ collation: e.target.value || null })}
          />
        </label>
      </div>
      <label className="checkbox">
        <input
          type="checkbox"
          checked={field.nullDefault}
          onChange={(e) =>
            onChange({
              nullDefault: e.target.checked,
              default: e.target.checked ? null : field.default,
            })
          }
        />
        DEFAULT NULL
      </label>
      <label>
        Khóa ngoại
        <Select
          label="Cột đích tương thích"
          value={field.reference?.slice(0, 3).join(".") ?? ""}
          options={[
            { value: "", label: "Không dùng khóa ngoại" },
            ...options.map((r) => ({
              value: r.slice(0, 3).join("."),
              label: r.slice(0, 3).join("."),
            })),
          ]}
          onChange={(v) =>
            onChange({
              reference:
                options
                  .find((r) => r.slice(0, 3).join(".") === v)
                  ?.map((x) => x ?? "") ?? null,
            })
          }
        />
      </label>
    </div>
  );
}
