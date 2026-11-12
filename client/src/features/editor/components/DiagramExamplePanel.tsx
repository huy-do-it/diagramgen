import { useState } from 'react';
import type { DiagramTemplate } from '../templates/diagramTemplates';

interface DiagramExamplePanelProps {
  typeLabel: string;
  template: DiagramTemplate;
  canUseExample: boolean;
  onUseExample: () => void;
}

/** Chép từ v1; chỉ đổi chữ sang tiếng Việt. */
export function DiagramExamplePanel({ typeLabel, template, canUseExample, onUseExample }: DiagramExamplePanelProps) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(template.example.json);
      setCopied(true);
      setTimeout(() => setCopied(false), 2500);
    } catch {
      // Trình duyệt chặn clipboard: người dùng vẫn bôi đen ô ví dụ để copy thủ công
    }
  }

  return (
    <div>
      <h3 className="example-title">Cách điền model {typeLabel}</h3>
      <ul className="example-guide">
        <li>
          Thay mọi chỗ trống <code>&lt;...&gt;</code> trong editor. Bấm <strong>Chỗ trống tiếp ▸</strong> để nhảy tới chỗ
          tiếp theo. <code>"title"</code> trong JSON tự đồng bộ theo ô Tên sơ đồ.
        </li>
        {template.guide.map((line) => (
          <li key={line}>{line}</li>
        ))}
      </ul>

      <div className="toolbar">
        <span className="field-label" style={{ margin: 0 }}>
          Ví dụ: {template.example.title}
        </span>
        <div className="toolbar-group">
          <button type="button" className="button button-secondary button-small" onClick={handleCopy}>
            {copied ? '✓ Đã copy' : 'Copy ví dụ'}
          </button>
          <button
            type="button"
            className="button button-secondary button-small"
            onClick={onUseExample}
            disabled={!canUseExample}
          >
            Dùng ví dụ này
          </button>
        </div>
      </div>

      <textarea readOnly className="plantuml-code" value={template.example.json} aria-label={`Ví dụ JSON ${typeLabel}`} />
    </div>
  );
}
