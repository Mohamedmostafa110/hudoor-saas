import React, { useEffect, useState } from 'react';
import { useLanguage } from '../../context/LanguageContext';
import { api, ApiError } from '../../services/apiClient';
import type { AcademicGroup, ResetPasswordResponse, StudentCreatedResponse, StudentSummary } from '../../types';

export const StudentsManagementView: React.FC = () => {
  const { t } = useLanguage();

  const [students, setStudents] = useState<StudentSummary[]>([]);
  const [groups, setGroups] = useState<AcademicGroup[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [gradeFilter, setGradeFilter] = useState('');

  // Modals state
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [createdStudent, setCreatedStudent] = useState<StudentCreatedResponse | null>(null);
  const [resetResult, setResetResult] = useState<ResetPasswordResponse | null>(null);

  // Add form fields
  const [fullName, setFullName] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [parentPhoneNumber, setParentPhoneNumber] = useState('');
  const [currentGrade, setCurrentGrade] = useState('1st Secondary');
  const [initialGroupId, setInitialGroupId] = useState('');
  const [notes, setNotes] = useState('');
  const [formLoading, setFormLoading] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const loadData = async () => {
    try {
      const [studentsRes, groupsRes] = await Promise.all([
        api.get<StudentSummary[]>(`/teacher/students?search=${encodeURIComponent(search)}&grade=${encodeURIComponent(gradeFilter)}`),
        api.get<AcademicGroup[]>('/teacher/groups'),
      ]);
      setStudents(studentsRes);
      setGroups(groupsRes);
    } catch (err) {
      console.error('Failed to load students', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [search, gradeFilter]);

  const handleAddStudent = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormLoading(true);
    setFormError(null);

    try {
      const res = await api.post<StudentCreatedResponse>('/teacher/students', {
        fullName: fullName.trim(),
        phoneNumber: phoneNumber.trim() || undefined,
        parentPhoneNumber: parentPhoneNumber.trim() || undefined,
        currentGrade: currentGrade.trim() || undefined,
        initialGroupId: initialGroupId || undefined,
        notes: notes.trim() || undefined,
      });

      setCreatedStudent(res);
      setFullName('');
      setPhoneNumber('');
      setParentPhoneNumber('');
      setNotes('');
      await loadData();
    } catch (err) {
      if (err instanceof ApiError) {
        setFormError(err.message);
      } else {
        setFormError(t('actionError'));
      }
    } finally {
      setFormLoading(false);
    }
  };

  const handleResetPassword = async (student: StudentSummary) => {
    if (!window.confirm(`هل تريد إعادة تعيين كلمة المرور للطالب "${student.fullName}"؟ سيتم إبطال أي جلسات نشطة له فورًا.`)) {
      return;
    }

    try {
      const res = await api.post<ResetPasswordResponse>(`/teacher/students/${student.id}/reset-password`);
      setResetResult(res);
    } catch (err) {
      console.error('Failed to reset password', err);
      alert('فشل إعادة تعيين كلمة المرور.');
    }
  };

  const handleToggleStatus = async (student: StudentSummary) => {
    try {
      await api.patch(`/teacher/students/${student.id}/status`, {
        isActive: !student.isActive,
      });
      await loadData();
    } catch (err) {
      console.error('Failed to toggle student status', err);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Top Header & Search Filter */}
      <div className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <h2 style={{ fontSize: '1.4rem' }}>{t('students')} ({students.length} طالب)</h2>
          <p style={{ fontSize: '0.88rem' }}>إدارة بيانات الطلاب، توليد الأكواد وإعادة تعيين كلمات المرور في 5 ثوانٍ</p>
        </div>

        <button
          type="button"
          className="btn btn-primary"
          onClick={() => { setCreatedStudent(null); setIsAddModalOpen(true); }}
        >
          <span>➕</span> {t('addStudentBtn')}
        </button>
      </div>

      {/* Filter Row */}
      <div className="card" style={{ padding: '1rem', display: 'flex', gap: '1rem', flexWrap: 'wrap' }}>
        <input
          type="text"
          className="form-input"
          placeholder={t('search')}
          value={search}
          onChange={e => setSearch(e.target.value)}
          style={{ flex: 1, minWidth: '220px' }}
        />
        <select
          className="form-select"
          value={gradeFilter}
          onChange={e => setGradeFilter(e.target.value)}
          style={{ minWidth: '180px' }}
        >
          <option value="">جميع الصفوف</option>
          <option value="1st Secondary">الصف الأول الثانوي</option>
          <option value="2nd Secondary">الصف الثاني الثانوي</option>
          <option value="3rd Secondary">الصف الثالث الثانوي</option>
        </select>
      </div>

      {/* Students Table */}
      <div className="card" style={{ overflowX: 'auto', padding: '1rem' }}>
        {loading ? (
          <p>جاري تحميل قائمة الطلاب...</p>
        ) : students.length === 0 ? (
          <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-muted)' }}>
            لم يتم العثور على طلاب مطابقين للبحث.
          </p>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'start' }}>
            <thead>
              <tr style={{ borderBottom: '2px solid var(--border-color)', color: 'var(--text-secondary)' }}>
                <th style={{ padding: '0.75rem', textAlign: 'start' }}>كود الطالب</th>
                <th style={{ padding: '0.75rem', textAlign: 'start' }}>اسم الطالب</th>
                <th style={{ padding: '0.75rem', textAlign: 'start' }}>الصف</th>
                <th style={{ padding: '0.75rem', textAlign: 'start' }}>رقم الهاتف</th>
                <th style={{ padding: '0.75rem', textAlign: 'center' }}>المجموعات</th>
                <th style={{ padding: '0.75rem', textAlign: 'center' }}>الحالة</th>
                <th style={{ padding: '0.75rem', textAlign: 'center' }}>إجراءات</th>
              </tr>
            </thead>
            <tbody>
              {students.map(st => (
                <tr key={st.id} style={{ borderBottom: '1px solid var(--border-color-subtle)' }}>
                  <td style={{ padding: '0.85rem', fontWeight: 800, color: 'var(--primary-accent)' }}>
                    {st.studentCode}
                  </td>
                  <td style={{ padding: '0.85rem', fontWeight: 700 }}>{st.fullName}</td>
                  <td style={{ padding: '0.85rem' }}>{st.currentGrade || '—'}</td>
                  <td style={{ padding: '0.85rem', direction: 'ltr', textAlign: 'end' }}>
                    {st.phoneNumber || '—'}
                  </td>
                  <td style={{ padding: '0.85rem', textAlign: 'center' }}>{st.enrolledGroupsCount}</td>
                  <td style={{ padding: '0.85rem', textAlign: 'center' }}>
                    <span className={`badge ${st.isActive ? 'badge-present' : 'badge-absent'}`}>
                      {st.isActive ? 'نشط' : 'معطل'}
                    </span>
                  </td>
                  <td style={{ padding: '0.85rem', textAlign: 'center' }}>
                    <div style={{ display: 'inline-flex', gap: '0.4rem' }}>
                      <button
                        type="button"
                        className="btn btn-outline"
                        style={{ minHeight: '34px', padding: '0.2rem 0.6rem', fontSize: '0.8rem' }}
                        onClick={() => handleResetPassword(st)}
                        title="إعادة تعيين كلمة المرور في 5 ثوانٍ"
                      >
                        🔑 Reset
                      </button>
                      <button
                        type="button"
                        className="btn btn-outline"
                        style={{ minHeight: '34px', padding: '0.2rem 0.6rem', fontSize: '0.8rem' }}
                        onClick={() => handleToggleStatus(st)}
                      >
                        {st.isActive ? 'تعطيل' : 'تفعيل'}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Add Student Modal */}
      {isAddModalOpen && (
        <div className="modal-overlay">
          <div className="modal-card">
            {createdStudent ? (
              <div>
                <h3 style={{ color: 'var(--status-present)', marginBottom: '0.5rem' }}>
                  ✓ تم تسجيل الطالب وتوليد حسابه بنجاح!
                </h3>
                <p style={{ fontSize: '0.9rem', marginBottom: '1.25rem' }}>
                  شارك بيانات الدخول التالية مع الطالب:
                </p>

                <div style={{ padding: '1.25rem', backgroundColor: 'var(--bg-secondary)', borderRadius: 'var(--border-radius-md)', border: '2px solid var(--primary-accent)', marginBottom: '1.5rem' }}>
                  <div style={{ marginBottom: '0.75rem' }}>
                    <span style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>اسم الطالب:</span>
                    <h4 style={{ fontSize: '1.1rem' }}>{createdStudent.fullName}</h4>
                  </div>
                  <div style={{ marginBottom: '0.75rem' }}>
                    <span style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>كود الدخول الموحد (Student Code):</span>
                    <div style={{ fontSize: '1.35rem', fontWeight: 800, color: 'var(--primary-accent)', letterSpacing: '1px' }}>
                      {createdStudent.studentCode}
                    </div>
                  </div>
                  <div>
                    <span style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>كلمة المرور المؤقتة (مرة واحدة فقط):</span>
                    <div style={{ fontSize: '1.35rem', fontWeight: 800, color: 'var(--status-late)', letterSpacing: '1px' }}>
                      {createdStudent.temporaryPassword}
                    </div>
                  </div>
                </div>

                <button
                  type="button"
                  className="btn btn-primary"
                  style={{ width: '100%' }}
                  onClick={() => setIsAddModalOpen(false)}
                >
                  تم، إغلاق النافذة
                </button>
              </div>
            ) : (
              <div>
                <h3 style={{ marginBottom: '1rem' }}>تسجيل طالب جديد</h3>
                {formError && (
                  <div style={{ padding: '0.75rem', borderRadius: 'var(--border-radius-md)', backgroundColor: 'var(--status-absent-subtle)', color: 'var(--status-absent)', marginBottom: '1rem', fontSize: '0.9rem', fontWeight: 600 }}>
                    {formError}
                  </div>
                )}
                <form onSubmit={handleAddStudent}>
                  <div className="form-group">
                    <label className="form-label">الاسم الكامل للطالب *</label>
                    <input
                      type="text"
                      className="form-input"
                      value={fullName}
                      onChange={e => setFullName(e.target.value)}
                      required
                      placeholder="مثال: أحمد مصطفى إبراهيم"
                    />
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                    <div className="form-group">
                      <label className="form-label">رقم الهاتف</label>
                      <input
                        type="text"
                        className="form-input"
                        value={phoneNumber}
                        onChange={e => setPhoneNumber(e.target.value)}
                        placeholder="01012345678"
                      />
                    </div>
                    <div className="form-group">
                      <label className="form-label">رقم ولي الأمر</label>
                      <input
                        type="text"
                        className="form-input"
                        value={parentPhoneNumber}
                        onChange={e => setParentPhoneNumber(e.target.value)}
                        placeholder="01112345678"
                      />
                    </div>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                    <div className="form-group">
                      <label className="form-label">الصف الدراسي</label>
                      <select
                        className="form-select"
                        value={currentGrade}
                        onChange={e => setCurrentGrade(e.target.value)}
                      >
                        <option value="1st Secondary">الصف الأول الثانوي</option>
                        <option value="2nd Secondary">الصف الثاني الثانوي</option>
                        <option value="3rd Secondary">الصف الثالث الثانوي</option>
                      </select>
                    </div>
                    <div className="form-group">
                      <label className="form-label">تسكين في مجموعة (اختياري)</label>
                      <select
                        className="form-select"
                        value={initialGroupId}
                        onChange={e => setInitialGroupId(e.target.value)}
                      >
                        <option value="">-- بدون مجموعة الآن --</option>
                        {groups.map(g => (
                          <option key={g.id} value={g.id}>
                            {g.name} ({g.subjectName})
                          </option>
                        ))}
                      </select>
                    </div>
                  </div>

                  <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end', marginTop: '1.25rem' }}>
                    <button
                      type="button"
                      className="btn btn-secondary"
                      onClick={() => setIsAddModalOpen(false)}
                      disabled={formLoading}
                    >
                      {t('cancel')}
                    </button>
                    <button
                      type="submit"
                      className="btn btn-primary"
                      disabled={formLoading}
                    >
                      {formLoading ? 'جاري الإنشاء...' : 'حفظ وتوليد الكود'}
                    </button>
                  </div>
                </form>
              </div>
            )}
          </div>
        </div>
      )}

      {/* 5-Second Password Reset Result Modal (Commercial Covenant #5) */}
      {resetResult && (
        <div className="modal-overlay">
          <div className="modal-card">
            <h3 style={{ color: 'var(--status-late)', marginBottom: '0.5rem' }}>
              🔑 تم إعادة تعيين كلمة المرور بنجاح (في 5 ثوانٍ)
            </h3>
            <p style={{ fontSize: '0.9rem', marginBottom: '1.25rem' }}>
              تم إبطال جميع الجلسات النشطة القديمة للطالب على أي جهاز، وتوليد كلمة المرور المؤقتة التالية:
            </p>

            <div style={{ padding: '1.5rem', backgroundColor: 'var(--bg-secondary)', borderRadius: 'var(--border-radius-md)', border: '2px solid var(--status-late)', textAlign: 'center', marginBottom: '1.5rem' }}>
              <div style={{ fontSize: '0.9rem', color: 'var(--text-muted)', marginBottom: '0.35rem' }}>كلمة المرور المؤقتة الجديدة:</div>
              <div style={{ fontSize: '2rem', fontWeight: 800, color: 'var(--primary-accent)', letterSpacing: '2px' }}>
                {resetResult.temporaryPassword}
              </div>
              <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginTop: '0.5rem' }}>
                صالحة لمدة 7 أيام وسيلتزم الطالب بتغييرها فور تسجيل الدخول.
              </div>
            </div>

            <button
              type="button"
              className="btn btn-primary"
              style={{ width: '100%' }}
              onClick={() => setResetResult(null)}
            >
              تم النسخ وإبلاغ الطالب
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
