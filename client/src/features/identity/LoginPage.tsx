import { useState, type FormEvent } from 'react';
import { Navigate, useNavigate, Link, useLocation } from 'react-router';
import { useAuth } from '../../auth/AuthContext';
import { ApiError } from '../../shared/api';

export function LoginPage() {
  const { user, notice, signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const registrationNotice = typeof location.state?.notice === 'string' ? location.state.notice : null;

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  if (user) {
    return <Navigate to="/projects" replace />;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!username.trim() || !password) {
      setErrorMessage('Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.');
      return;
    }

    setSubmitting(true);
    setErrorMessage(null);
    try {
      await signIn(username, password);
      navigate('/projects', { replace: true });
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setErrorMessage(err.message);
      } else {
        setErrorMessage('Đăng nhập thất bại. Vui lòng kiểm tra lại kết nối mạng.');
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="login-page auth-page">
      <div className="login-story">
        <div className="brand">
          <div className="brand-mark">📐</div>
          <span>UMLForge</span>
        </div>

        <div className="story-copy">
          <div className="eyebrow">DISTRIBUTED COLLABORATION PLATFORM</div>
          <h1>Thiết kế, Kiểm tra &amp; Thẩm định UML Chuẩn mực</h1>
          <p>
            Nền tảng hỗ trợ sinh viên và giảng viên thiết kế, render tự động bằng PlantUML và kiểm tra luật UML theo thời gian thực.
          </p>

          <ul className="model-flow">
            <li>
              <span>MICROSERVICES</span>
              .NET 8 &amp; Redis Streams
            </li>
            <li>
              <span>gRPC ENGINE</span>
              Tự động bắt lỗi UML
            </li>
            <li>
              <span>PLANTUML</span>
              Render SVG tức thì
            </li>
          </ul>
        </div>

        <div className="story-footer">
          <span>PRN232 Final Assignment — FPT University</span>
          <span>Phiên bản v2.0</span>
        </div>
      </div>

      <div className="login-panel">
        <div className="login-form-wrap">
          <span className="welcome-tag">CHÀO MỪNG TRỞ LẠI</span>
          <h2>Đăng nhập hệ thống</h2>
          <p className="muted">Nhập thông tin tài khoản của bạn để tiếp tục vào không gian làm việc.</p>

          {registrationNotice && <div className="notice success" role="status">{registrationNotice}</div>}
          {notice && <div className="notice" role="status" style={{ marginBottom: 16 }}>{notice}</div>}
          {errorMessage && <div id="login-error" role="alert" className="form-error" style={{ marginBottom: 16 }}>{errorMessage}</div>}

          <form onSubmit={handleSubmit} aria-busy={submitting}>
            <label htmlFor="username">Tên đăng nhập</label>
            <input
              id="username"
              aria-invalid={!!errorMessage}
              aria-describedby={errorMessage ? 'login-error' : undefined}
              type="text"
              autoComplete="username"
              placeholder="Nhập tên đăng nhập..."
              value={username}
              onChange={e => setUsername(e.target.value)}
              disabled={submitting}
              required
            />

            <label htmlFor="password">Mật khẩu</label>
            <div className="password-field">
              <input
                id="password"
                aria-invalid={!!errorMessage}
                aria-describedby={errorMessage ? 'login-error' : undefined}
                type={showPassword ? 'text' : 'password'}
                autoComplete="current-password"
                placeholder="Nhập mật khẩu..."
                value={password}
                onChange={e => setPassword(e.target.value)}
                disabled={submitting}
                required
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                aria-pressed={showPassword}
                aria-controls="password"
              >
                {showPassword ? 'Ẩn' : 'Hiện'}
              </button>
            </div>

            <button
              type="submit"
              className="button button-primary login-submit"
              disabled={submitting}
              aria-busy={submitting}
            >
              <span>{submitting ? 'Đang xác thực...' : 'Đăng nhập'}</span>
              <span>&rarr;</span>
            </button>
          </form>

          <div className="login-help">
            Chưa có tài khoản?{' '}
            <Link to="/register" style={{ fontWeight: 600 }}>
              Đăng ký tài khoản mới
            </Link>
          </div>
        </div>
      </div>
    </main>
  );
}

