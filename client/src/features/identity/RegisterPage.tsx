import { useState, type FormEvent } from 'react';
import { Navigate, useNavigate, Link } from 'react-router';
import { useAuth } from '../../auth/AuthContext';
import { ApiError } from '../../shared/api';
import { identityApi } from './identityApi';

export function RegisterPage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const [username, setUsername] = useState('');
  const [fullName, setFullName] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [role, setRole] = useState<'Student' | 'Lecturer'>('Student');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  if (user) {
    return <Navigate to="/projects" replace />;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrorMessage(null);

    if (!username.trim() || !fullName.trim() || !password) {
      setErrorMessage('Vui lòng điền đầy đủ tất cả các trường.');
      return;
    }

    if (password.length < 8) {
      setErrorMessage('Mật khẩu phải có độ dài tối thiểu 8 ký tự (theo quy chuẩn bảo mật BR-02).');
      return;
    }

    if (password !== confirmPassword) {
      setErrorMessage('Mật khẩu xác nhận không khớp.');
      return;
    }

    setSubmitting(true);
    try {
      await identityApi.register({
        username: username.trim(),
        fullName: fullName.trim(),
        password,
        role: role,
      });

      // Điều hướng về Login
      navigate('/login', {
        state: { notice: 'Đăng ký tài khoản thành công! Vui lòng đăng nhập để bắt đầu.' },
        replace: true,
      });
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setErrorMessage(err.message);
      } else {
        setErrorMessage('Đăng ký không thành công. Vui lòng thử lại sau.');
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
          <div className="eyebrow">TẠO TÀI KHOẢN MỚI</div>
          <h1>Gia nhập nền tảng Thiết kế UMLForge</h1>
          <p>
            Đăng ký để tạo dự án môn học, mời các thành viên cùng nhóm xây dựng sơ đồ UML, hoặc tham gia thẩm định đồ án với vai trò Giảng viên.
          </p>
          <ul className="model-flow">
            <li>
              <span>SINH VIÊN</span>
              Tạo dự án &amp; Vẽ UML
            </li>
            <li>
              <span>GIẢNG VIÊN</span>
              Nhận xét &amp; Thẩm định
            </li>
          </ul>
        </div>

        <div className="story-footer">
          <span>PRN232 Final Assignment — FPT University</span>
        </div>
      </div>

      <div className="login-panel">
        <div className="login-form-wrap" style={{ maxWidth: 420 }}>
          <span className="welcome-tag">KHỞI TẠO TÀI KHOẢN</span>
          <h2>Đăng ký thành viên</h2>
          <p className="muted">Điền thông tin bên dưới để bắt đầu sử dụng hệ thống.</p>

          {errorMessage && <div id="register-error" role="alert" className="form-error" style={{ marginBottom: 16 }}>{errorMessage}</div>}

          <form onSubmit={handleSubmit} aria-busy={submitting}>
            <label htmlFor="reg-username">Tên đăng nhập</label>
            <input
              id="reg-username"
              aria-invalid={!!errorMessage}
              aria-describedby={errorMessage ? 'register-error' : undefined}
              type="text"
              autoComplete="username"
              placeholder="Ví dụ: student1, huydd..."
              value={username}
              onChange={e => setUsername(e.target.value)}
              disabled={submitting}
              required
            />

            <label htmlFor="reg-fullname">Họ và tên</label>
            <input
              id="reg-fullname"
              aria-invalid={!!errorMessage}
              aria-describedby={errorMessage ? 'register-error' : undefined}
              type="text"
              autoComplete="name"
              placeholder="Ví dụ: Đỗ Đức Huy"
              value={fullName}
              onChange={e => setFullName(e.target.value)}
              disabled={submitting}
              required
            />

            <label htmlFor="reg-role">Vai trò đăng ký</label>
            <select
              id="reg-role"
              className="select"
              value={role}
              onChange={e => setRole(e.target.value as 'Student' | 'Lecturer')}
              disabled={submitting}
              style={{ width: '100%', maxWidth: '100%' }}
            >
              <option value="Student">Sinh viên (Student)</option>
              <option value="Lecturer">Giảng viên (Lecturer)</option>
            </select>

            <label htmlFor="reg-password">Mật khẩu (≥ 8 ký tự)</label>
            <div className="password-field">
              <input
                id="reg-password"
                aria-invalid={!!errorMessage}
                aria-describedby={errorMessage ? 'register-error' : undefined}
                type={showPassword ? 'text' : 'password'}
                autoComplete="new-password"
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
                aria-controls="reg-password reg-confirm"
              >
                {showPassword ? 'Ẩn' : 'Hiện'}
              </button>
            </div>

            <label htmlFor="reg-confirm">Xác nhận mật khẩu</label>
            <input
              id="reg-confirm"
              aria-invalid={!!errorMessage}
              aria-describedby={errorMessage ? 'register-error' : undefined}
              type={showPassword ? 'text' : 'password'}
              autoComplete="new-password"
              placeholder="Nhập lại mật khẩu..."
              value={confirmPassword}
              onChange={e => setConfirmPassword(e.target.value)}
              disabled={submitting}
              aria-busy={submitting}
              required
            />

            <button
              type="submit"
              className="button button-primary login-submit"
              disabled={submitting}
            >
              <span>{submitting ? 'Đang khởi tạo...' : 'Đăng ký ngay'}</span>
              <span>&rarr;</span>
            </button>
          </form>

          <div className="login-help">
            Đã có tài khoản?{' '}
            <Link to="/login" style={{ fontWeight: 600 }}>
              Đăng nhập tại đây
            </Link>
          </div>
        </div>
      </div>
    </main>
  );
}
