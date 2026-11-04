import React, { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { useLanguage } from '../context/LanguageContext';
import { api, ApiError } from '../services/apiClient';

interface PasswordChangeModalProps {
  isOpen: boolean;
  onClose: () => void;
  isMandatory?: boolean;
}

export const PasswordChangeModal: React.FC<PasswordChangeModalProps> = ({
  isOpen,
  onClose,
  isMandatory = false,
}) => {
  const { user, updateMustChangePassword } = useAuth();
  const { t } = useLanguage();

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    if (newPassword !== confirmPassword) {
      setError('كلمة المرور الجديدة وتأكيدها غير متطابقين');
      return;
    }

    setLoading(true);

    try {
      const endpoint = user?.role === 'Student'
        ? '/student/change-password'
        : '/teacher/change-password';

      const res = await api.post<{ message: string; token?: string }>(endpoint, {
        currentPassword,
        newPassword,
      });

      if (res.token) {
        localStorage.setItem('hudoor_token', res.token);
      }

      updateMustChangePassword(false);
      setSuccess('تم تغيير كلمة المرور بنجاح!');
      setTimeout(() => {
        onClose();
      }, 1000);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError(t('actionError'));
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="modal-overlay">
      <div className="modal-card">
        <h3 style={{ marginBottom: '0.5rem' }}>
          {isMandatory ? 'تغيير كلمة المرور المؤقتة (إلزامي)' : t('changePassword')}
        </h3>
        <p style={{ fontSize: '0.9rem', marginBottom: '1.25rem' }}>
          {isMandatory
            ? 'تم تسجيل دخولك بكلمة مرور مؤقتة، يرجى تعيين كلمة مرور جديدة للاستمرار.'
            : 'أدخل كلمة المرور الحالية ثم كلمة المرور الجديدة.'}
        </p>

        {error && (
          <div style={{ padding: '0.75rem', borderRadius: 'var(--border-radius-md)', backgroundColor: 'var(--status-absent-subtle)', color: 'var(--status-absent)', marginBottom: '1rem', fontSize: '0.9rem', fontWeight: 600 }}>
            {error}
          </div>
        )}

        {success && (
          <div style={{ padding: '0.75rem', borderRadius: 'var(--border-radius-md)', backgroundColor: 'var(--status-present-subtle)', color: 'var(--status-present)', marginBottom: '1rem', fontSize: '0.9rem', fontWeight: 600 }}>
            {success}
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label className="form-label">كلمة المرور الحالية / المؤقتة</label>
            <input
              type="password"
              className="form-input"
              value={currentPassword}
              onChange={e => setCurrentPassword(e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label className="form-label">كلمة المرور الجديدة</label>
            <input
              type="password"
              className="form-input"
              value={newPassword}
              onChange={e => setNewPassword(e.target.value)}
              required
              minLength={6}
            />
          </div>

          <div className="form-group">
            <label className="form-label">تأكيد كلمة المرور الجديدة</label>
            <input
              type="password"
              className="form-input"
              value={confirmPassword}
              onChange={e => setConfirmPassword(e.target.value)}
              required
              minLength={6}
            />
          </div>

          <div style={{ display: 'flex', gap: '0.75rem', marginTop: '1.5rem', justifyContent: 'flex-end' }}>
            {!isMandatory && (
              <button
                type="button"
                className="btn btn-secondary"
                onClick={onClose}
                disabled={loading}
              >
                {t('cancel')}
              </button>
            )}
            <button
              type="submit"
              className="btn btn-primary"
              disabled={loading}
            >
              {loading ? 'جاري الحفظ...' : t('save')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
