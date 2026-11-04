import React, { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { useLanguage } from '../context/LanguageContext';
import { api, ApiError } from '../services/apiClient';
import type { AuthResponse, UserRole } from '../types';

export const LoginView: React.FC = () => {
  const { login } = useAuth();
  const { t } = useLanguage();

  const [activeTab, setActiveTab] = useState<UserRole>('Teacher');
  const [username, setUsername] = useState('');
  const [studentCode, setStudentCode] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);

    try {
      let endpoint = '/auth/teacher/login';
      let payload: any = { password };

      if (activeTab === 'Teacher') {
        endpoint = '/auth/teacher/login';
        payload.username = username.trim();
      } else if (activeTab === 'Student') {
        endpoint = '/auth/student/login';
        payload.studentCode = studentCode.trim().toUpperCase();
      } else if (activeTab === 'PlatformAdmin') {
        endpoint = '/auth/admin/login';
        payload.username = username.trim();
      }

      const res = await api.post<AuthResponse>(endpoint, payload);
      login(res);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.message === 'Invalid credentials.') {
          setError('اسم المستخدم أو كلمة المرور غير صحيحة');
        } else {
          setError(err.message);
        }
      } else {
        setError(t('actionError'));
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: '80vh', padding: '1rem' }}>
      <div className="card" style={{ maxWidth: '440px', width: '100%', padding: '2rem' }}>
        <div style={{ textAlign: 'center', marginBottom: '1.75rem' }}>
          <img src="/logo.svg" alt="Hudoor Logo" style={{ width: '64px', height: '64px', borderRadius: '16px', margin: '0 auto 1rem auto', display: 'block', boxShadow: '0 8px 24px rgba(197, 155, 39, 0.3)' }} />
          <h2 style={{ fontSize: '1.5rem', marginBottom: '0.4rem' }}>{t('brandName')}</h2>
          <p style={{ fontSize: '0.9rem' }}>{t('brandTagline')}</p>
        </div>

        {/* Tab Switcher */}
        <div style={{ display: 'flex', gap: '0.4rem', backgroundColor: 'var(--bg-secondary)', padding: '0.35rem', borderRadius: 'var(--border-radius-md)', marginBottom: '1.5rem' }}>
          <button
            type="button"
            className="btn"
            style={{
              flex: 1,
              minHeight: '40px',
              backgroundColor: activeTab === 'Teacher' ? 'var(--bg-surface)' : 'transparent',
              color: activeTab === 'Teacher' ? 'var(--primary-accent)' : 'var(--text-muted)',
              boxShadow: activeTab === 'Teacher' ? 'var(--shadow-sm)' : 'none',
            }}
            onClick={() => { setActiveTab('Teacher'); setError(null); }}
          >
            {t('teacherTab')}
          </button>
          <button
            type="button"
            className="btn"
            style={{
              flex: 1,
              minHeight: '40px',
              backgroundColor: activeTab === 'Student' ? 'var(--bg-surface)' : 'transparent',
              color: activeTab === 'Student' ? 'var(--primary-accent)' : 'var(--text-muted)',
              boxShadow: activeTab === 'Student' ? 'var(--shadow-sm)' : 'none',
            }}
            onClick={() => { setActiveTab('Student'); setError(null); }}
          >
            {t('studentTab')}
          </button>
          <button
            type="button"
            className="btn"
            style={{
              flex: 1,
              minHeight: '40px',
              backgroundColor: activeTab === 'PlatformAdmin' ? 'var(--bg-surface)' : 'transparent',
              color: activeTab === 'PlatformAdmin' ? 'var(--primary-accent)' : 'var(--text-muted)',
              boxShadow: activeTab === 'PlatformAdmin' ? 'var(--shadow-sm)' : 'none',
            }}
            onClick={() => { setActiveTab('PlatformAdmin'); setError(null); }}
          >
            {t('adminTab')}
          </button>
        </div>

        {error && (
          <div style={{ padding: '0.75rem 1rem', borderRadius: 'var(--border-radius-md)', backgroundColor: 'var(--status-absent-subtle)', color: 'var(--status-absent)', marginBottom: '1.25rem', fontSize: '0.9rem', fontWeight: 600 }}>
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit}>
          {activeTab === 'Student' ? (
            <div className="form-group">
              <label className="form-label">{t('studentCodeLabel')}</label>
              <input
                type="text"
                className="form-input"
                placeholder="HD-XXXXXX"
                value={studentCode}
                onChange={e => setStudentCode(e.target.value)}
                required
                autoFocus
              />
            </div>
          ) : (
            <div className="form-group">
              <label className="form-label">{t('usernameLabel')}</label>
              <input
                type="text"
                className="form-input"
                value={username}
                onChange={e => setUsername(e.target.value)}
                required
                autoFocus
              />
            </div>
          )}

          <div className="form-group">
            <label className="form-label">{t('passwordLabel')}</label>
            <input
              type="password"
              className="form-input"
              value={password}
              onChange={e => setPassword(e.target.value)}
              required
            />
          </div>

          <button
            type="submit"
            className="btn btn-primary"
            style={{ width: '100%', marginTop: '1rem', minHeight: '48px', fontSize: '1.05rem' }}
            disabled={loading}
          >
            {loading ? t('loggingIn') : t('loginBtn')}
          </button>
        </form>
      </div>
    </div>
  );
};
