import { useEffect, useRef, useState } from "react";
import { ChevronDown } from "lucide-react";
import { Button, Modal } from "../ui";

const commonTypes = [
  "VARCHAR(16)",
  "VARCHAR(32)",
  "VARCHAR(128)",
  "VARCHAR(256)",
  "VARCHAR(MAX)",
  "NVARCHAR(16)",
  "NVARCHAR(32)",
  "NUMERIC(19, 4)",
  "NVARCHAR(256)",
  "NVARCHAR(MAX)",
  "SMALLDATETIME",
  "INT",
  "SMALLINT",
  "TINYINT",
  "BIT",
  "DATETIME",
  "DATE",
  "BIGINT",
  "DECIMAL(19, 4)",
  "DATETIME2",
  "DATETIMEOFFSET",
  "TIME",
  "CHAR(1)",
  "NCHAR(1)",
  "BINARY(16)",
  "VARBINARY(MAX)",
  "FLOAT",
  "REAL",
  "MONEY",
  "SMALLMONEY",
  "UNIQUEIDENTIFIER",
  "TEXT",
  "NTEXT",
  "IMAGE",
  "ROWVERSION",
  "TIMESTAMP",
  "XML",
];
const typeDescriptions: Record<string, string> = {
  "VARCHAR(16)": "Chuỗi ký tự tối đa 16 ký tự",
  "VARCHAR(32)": "Chuỗi ký tự tối đa 32 ký tự",
  "VARCHAR(128)": "Chuỗi ký tự tối đa 128 ký tự",
  "VARCHAR(256)": "Chuỗi ký tự tối đa 256 ký tự",
  "VARCHAR(MAX)": "Chuỗi ký tự độ dài lớn",
  "NVARCHAR(16)": "Chuỗi Unicode tối đa 16 ký tự",
  "NVARCHAR(32)": "Chuỗi Unicode tối đa 32 ký tự",
  "NUMERIC(19, 4)": "Số thập phân 19 chữ số, 4 số lẻ",
  "NVARCHAR(256)": "Chuỗi Unicode tối đa 256 ký tự",
  "NVARCHAR(MAX)": "Chuỗi Unicode độ dài lớn",
  "SMALLDATETIME": "Ngày và giờ độ chính xác thấp",
  INT: "Số nguyên 32 bit",
  SMALLINT: "Số nguyên 16 bit",
  TINYINT: "Số nguyên từ 0 đến 255",
  BIT: "Đúng hoặc sai",
  DATETIME: "Ngày và giờ",
  DATE: "Ngày",
  BIGINT: "Số nguyên 64 bit",
  "DECIMAL(19, 4)": "Số thập phân chính xác",
  DATETIME2: "Ngày và giờ độ chính xác cao",
  DATETIMEOFFSET: "Ngày và giờ kèm múi giờ",
  TIME: "Thời gian trong ngày",
  "CHAR(1)": "Chuỗi ký tự cố định",
  "NCHAR(1)": "Chuỗi Unicode cố định",
  "BINARY(16)": "Dữ liệu nhị phân cố định",
  "VARBINARY(MAX)": "Dữ liệu nhị phân độ dài lớn",
  FLOAT: "Số thực dấu phẩy động",
  REAL: "Số thực độ chính xác đơn",
  MONEY: "Kiểu tiền tệ",
  SMALLMONEY: "Kiểu tiền tệ độ chính xác thấp",
  UNIQUEIDENTIFIER: "Mã định danh duy nhất",
  TEXT: "Văn bản dài không Unicode",
  NTEXT: "Văn bản dài Unicode",
  IMAGE: "Dữ liệu hình ảnh nhị phân",
  ROWVERSION: "Giá trị phiên bản dòng",
  TIMESTAMP: "Dấu thời gian phiên bản dòng",
  XML: "Dữ liệu XML",
};

function DataTypePicker({ value, disabled, onChange }: {
  value: string;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  const root = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const filter = value.trim().toLowerCase();
  const options = commonTypes.filter((type) => !filter || type.toLowerCase().includes(filter));

  useEffect(() => {
    if (!open) return;
    const closeOutside = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener("pointerdown", closeOutside);
    return () => document.removeEventListener("pointerdown", closeOutside);
  }, [open]);

  return <div ref={root} className="table-data-type-picker">
    <div className="table-data-type-input">
      <input aria-label="Kiểu dữ liệu" placeholder="Chọn hoặc nhập kiểu dữ liệu" value={value}
        onFocus={() => setOpen(true)} onChange={(event) => { onChange(event.target.value); setOpen(true); }} disabled={disabled} />
      <button type="button" className="table-data-type-trigger" aria-label="Mở danh sách kiểu dữ liệu"
        aria-expanded={open} onMouseDown={(event) => event.preventDefault()} onClick={() => setOpen((current) => !current)} disabled={disabled}>
        <ChevronDown size={15} />
      </button>
    </div>
    {open && <div className="table-data-type-options" role="listbox" aria-label="Danh sách kiểu dữ liệu">
      {options.map((type) => <button type="button" role="option" aria-selected={type === value} key={type}
        onMouseDown={(event) => event.preventDefault()} onClick={() => { onChange(type); setOpen(false); }}>
        <span>{type}</span>
        <small>{typeDescriptions[type] ?? "Kiểu dữ liệu SQL Server"}</small>
      </button>)}
      {!options.length && <p role="status">Không có kiểu dữ liệu phù hợp.</p>}
    </div>}
  </div>;
}

export function TableColumnDialog({
  mode,
  column,
  onClose,
  onSubmit,
  busy,
}: {
  mode: "add" | "alter";
  column?: string;
  onClose: () => void;
  onSubmit: (name: string, dataType: string, nullable: boolean) => void;
  busy: boolean;
}) {
  const [name, setName] = useState(column ?? "");
  const [dataType, setDataType] = useState("NVARCHAR(16)");
  const [nullable, setNullable] = useState(true);
  const title = mode === "add" ? "Thêm cột" : "Sửa cột";

  return (
    <Modal title={title} className="table-column-modal" onClose={() => { if (!busy) onClose(); }}>
      <form className="table-column-form" onSubmit={(event) => {
        event.preventDefault();
        const normalizedType = dataType.trim().replace(/\s*,\s*/g, ",");
        if (name.trim() && normalizedType) onSubmit(name.trim(), normalizedType, nullable);
      }}>
        <label>
          Tên cột
          <input aria-label="Tên cột" placeholder="Nhập tên cột" value={name} onChange={(event) => setName(event.target.value)} autoFocus readOnly={mode === "alter"} disabled={busy} />
        </label>
        <label>
          Kiểu dữ liệu
          <DataTypePicker value={dataType} disabled={busy} onChange={setDataType} />
        </label>
        <label className="table-column-nullable">
          <input type="checkbox" checked={nullable} onChange={(event) => setNullable(event.target.checked)} disabled={busy} />
          Cho phép giá trị trống (NULL)
        </label>
        <div className="modal-footer">
          <Button type="button" disabled={busy} onClick={onClose}>Hủy</Button>
          <Button type="submit" variant="primary" disabled={busy || !name.trim() || !dataType.trim()}>Lưu</Button>
        </div>
      </form>
    </Modal>
  );
}
