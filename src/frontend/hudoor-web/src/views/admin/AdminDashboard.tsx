import React, { useEffect, useState } from 'react';
import { api, ApiError } from '../../services/apiClient';

interface TeacherItem {
  id: string;
  username: string;
  fullName: string;
  email: string;
  phoneNumber?: string;
  isActive: boolean;
  createdAtUtc: string;
}

export const AdminDashboard: React.FC = () => {
  const [teachers, setTeachers] = useState<TeacherItem[]>([]);
  const [loading, setLoading] = useState(true);

  // Create form
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [formLoading, setFormLoading] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const loadTeachers = async () => {
    try {
      const res = await api.get<TeacherItem[]>('/admin/teachers');
      setTeachers(res);
    } catch (err) {
      console.error('Failed to load teachers', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTeachers();
  }, []);

  const handleCreateTeacher = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormLoading(true);
    setFormError(null);

    try {
      await api.post('/admin/teachers', {
        username: username.trim(),
        password,
        fullName: fullName.trim(),
        email: email.trim(),
        phoneNumber: phoneNumber.trim() || undefined,
      });

      setIsModalOpen(false);
      setUsername('');
      setPassword('');
      setFullName('');
      setEmail('');
      setPhoneNumber('');
      await loadTeachers();
    } catch (err) {
      if (err instanceof ApiError) {
        setFormError(err.message);
      } else {
        setFormError('حدث خطأ أثناء إنشاء حساب المعلم');
      }
    } finally {
      setFormLoading(false);
    }
  };

  const handleToggleStatus = async (teacher: TeacherItem) => {
    try {
      await api.patch(`/admin/teachers/${teacher.id}/status`, {
        isActive: !teacher.isActive,
      });
      await loadTeachers();
    } catch (err) {
      console.error('Failed to toggle status', err);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      <div className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <h2 style={{ fontSize: '1.4rem' }}>إدارة مساحات عمل المعلمين (Platform Admin)</h2>
          <p style={{ fontSize: '0.88rem' }}>تهيئة وتفعيل حسابات المعلمين المعزولة (Commercial Covenant #1)</p>
        </div>

        <button
          type="button"
          className="btn btn-primary"
          onClick={() => setIsModalOpen(true)}
        >
          <span>➕</span> إنشاء حساب معلم جديد
        </button>
      </div>

      <div className="card" style={{ overflowX: 'auto', padding: '1rem' }}>
        {loading ? (
          <p>جاري تحميل قائمة المعلمين...</p>
        ) : teachers.length === 0 ? (
          <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-muted)' }}>
            لا يوجد معلمون مسجلون حاليًا.
          </p>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'start' }}>
            <thead>
              <tr style={{ borderBottom: '2px solid var(--border-color)', color: 'var(--text-secondary)' }}>
                <th style={{ padding: '0.75rem', textAlign: 'start' }}>اسم المستخدم</th>
                <th style={{ padding: '0.75rem', textAlign: 'start' }}>اسم المعلم</th>
                <th style={{ padding: '0.75rem', textAlign: 'start' }}>البريد الإلكتروني</th>
                <th style={{ padding: '0.75rem', textAlign: 'center' }}>الحالة</th>
                <th style={{ padding: '0.75rem', textAlign: 'center' }}>إجراءات</th>
              </tr>
            </thead>
            <tbody>
              {teachers.map(t => (
                <tr key={t.id} style={{ borderBottom: '1px solid var(--border-color-subtle)' }}>
                  <td style={{ padding: '0.85rem', fontWeight: 800, color: 'var(--primary-accent)' }}>
                    {t.username}
                  </td>
                  <td style={{ padding: '0.85rem', fontWeight: 700 }}>{t.fullName}</td>
                  <td style={{ padding: '0.85rem' }}>{t.email}</td>
                  <td style={{ padding: '0.85rem', textAlign: 'center' }}>
                    <span className={`badge ${t.isActive ? 'badge-present' : 'badge-absent'}`}>
                      {t.isActive ? 'نشط' : 'معطل'}
                    </span>
                  </td>
                  <td style={{ padding: '0.85rem', textAlign: 'center' }}>
                    <button
                      type="button"
                      className="btn btn-outline"
                      style={{ minHeight: '34px', padding: '0.2rem 0.6rem', fontSize: '0.8rem' }}
                      onClick={() => handleToggleStatus(t)}
                    >
                      {t.isActive ? 'تعطيل الحساب' : 'تفعيل الحساب'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {isModalOpen && (
        <div className="modal-overlay">
          <div className="modal-card">
            <h3 style={{ marginBottom: '1rem' }}>تهيئة مساحة عمل معلم جديد</h3>
            {formError && (
              <div style={{ padding: '0.75rem', borderRadius: 'var(--border-radius-md)', backgroundColor: 'var(--status-absent-subtle)', color: 'var(--status-absent)', marginBottom: '1rem', fontSize: '0.9rem', fontWeight: 600 }}>
                {formError}
              </div>
            )}
            <form onSubmit={handleCreateTeacher}>
              <div className="form-group">
                <label className="form-label">اسم المستخدم (Username) *</label>
                <input
                  type="text"
                  className="form-input"
                  value={username}
                  onChange={e => setUsername(e.target.value)}
                  required
                />
              </div>

              <div className="form-group">
                <label className="form-label">كلمة المرور الابتدائية *</label>
                <input
                  type="password"
                  className="form-input"
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                  required
                  minLength={8}
                />
              </div>

              <div className="form-group">
                <label className="form-label">الاسم الكامل *</label>
                <input
                  type="text"
                  className="form-input"
                  value={fullName}
                  onChange={e => setFullName(e.target.value)}
                  required
                />
              </div>

              <div className="form-group">
                <label className="form-label">البريد الإلكتروني *</label>
                <input
                  type="email"
                  className="form-input"
                  value={email}
                  onChange={e => setEmail(e.target.value)}
                  required
                />
              </div>

              <div className="form-group">
                <label className="form-label">رقم الهاتف</label>
                <input
                  type="text"
                  className="form-input"
                  value={phoneNumber}
                  onChange={e => setPhoneNumber(e.target.value)}
                />
              </div>

              <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end', marginTop: '1.25rem' }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setIsModalOpen(false)}
                  disabled={formLoading}
                >
                  إلغاء
                </button>
                <button
                  type="submit"
                  className="btn btn-primary"
                  disabled={formLoading}
                >
                  {formLoading ? 'جاري الإنشاء...' : 'إنشاء وتفعيل الحساب'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
