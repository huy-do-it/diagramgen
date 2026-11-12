import type { ReactNode } from 'react';
import { Brand } from './Brand';

interface AuthLayoutProps {
  title: string;
  description: string;
  children: ReactNode;
}

/** Khung chung cho UI-01 Đăng nhập và UI-02 Đăng ký. */
export function AuthLayout({ title, description, children }: AuthLayoutProps) {
  return <main className="login-page">
    <section className="login-story" aria-labelledby="product-title">
      <Brand />
      <div className="story-copy"><p className="eyebrow">CẢ NHÓM CÙNG LÀM</p>
        <h1 id="product-title">Nhập như điền bảng.<br />Sơ đồ tự vẽ.</h1>
        <p>Tạo, kiểm tra luật UML và render bộ 5 sơ đồ cho đồ án — giảng viên duyệt ngay trên hệ thống.</p>
        <ol className="model-flow" aria-label="Quy trình">
          <li><span>01</span>Model</li><li><span>02</span>Validate</li><li><span>03</span>Render</li>
        </ol>
      </div>
      <p className="story-footer">UMLForge <span>PRN232 · Team workspace</span></p>
    </section>
    <section className="login-panel" aria-labelledby="auth-title">
      <div className="login-form-wrap"><span className="welcome-tag">KHÔNG GIAN CỦA BẠN</span>
        <h2 id="auth-title">{title}</h2>
        <p className="muted">{description}</p>
        {children}
      </div>
    </section>
  </main>;
}
