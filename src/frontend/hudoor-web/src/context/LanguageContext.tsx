import React, { createContext, useContext, useEffect, useState } from 'react';

export type Language = 'ar' | 'en';

const translations: Record<Language, Record<string, string>> = {
  ar: {
    // Brand
    brandName: 'حضور',
    brandTagline: 'منصة الحضور الذكي للمدرسين والمراكز التعليمية',

    // Nav
    dashboard: 'لوحة التحكم',
    liveSession: 'جلسة حضور مباشرة',
    fastRoster: 'شبكة التحضير السريع',
    students: 'إدارة الطلاب',
    groups: 'المجموعات والمواد',
    reports: 'التقارير والإحصائيات',
    rollover: 'ترحيل العام الدراسي',
    logout: 'تسجيل الخروج',
    changePassword: 'تغيير كلمة المرور',
    welcome: 'مرحبًا بك',

    // Login
    loginTitle: 'تسجيل الدخول إلى منصة حضور',
    teacherTab: 'حساب المعلم',
    studentTab: 'حساب الطالب',
    adminTab: 'إدارة المنصة',
    usernameLabel: 'اسم المستخدم',
    studentCodeLabel: 'كود الطالب (HD-XXXXXX)',
    passwordLabel: 'كلمة المرور',
    loginBtn: 'دخول المنصة',
    loggingIn: 'جاري التحقق...',

    // Dashboard
    totalStudents: 'إجمالي الطلاب',
    activeGroups: 'المجموعات النشطة',
    totalSessions: 'الجلسات المنجزة',
    attendanceRate: 'معدل الحضور العام',
    atRiskStudents: 'طلاب بحاجة لمتابعة (غياب)',
    startLiveSessionBtn: 'بدء جلسة تفاعلية (QR)',
    addStudentBtn: 'تسجيل طالب جديد',

    // Attendance
    present: 'حاضر',
    late: 'متأخر',
    excused: 'معذور',
    absent: 'غائب',
    unrecorded: 'غير مسجل',
    markAllPresent: 'تحضير الكل حاضر',
    closeSessionBtn: 'إنهاء الجلسة وحصر الغياب',
    sessionClosedMsg: 'تم إنهاء الجلسة وتسجيل الغائبين تلقائيًا',
    scanQrNotice: 'قم بتوجيه كاميرا الهاتف نحو باركود الشاشة لتسجيل الحضور فورًا',
    tokenRotatingIn: 'يتجدد الرمز خلال',
    seconds: 'ثانية',

    // Student Portal
    studentPortalTitle: 'بوابة الحضور للطلاب',
    myAttendance: 'سجل حضوري',
    scanQrBtn: 'مسح باركود الحضور (QR)',
    mySummary: 'ملخص الحضور',
    myPercentage: 'نسبة الحضور الشخصية',

    // Actions & Common
    save: 'حفظ',
    cancel: 'إلغاء',
    search: 'بحث بالاسم أو الكود...',
    exportCsv: 'تصدير كشف Excel / CSV',
    actionSuccess: 'تمت العملية بنجاح',
    actionError: 'حدث خطأ، يرجى المحاولة لاحقًا',
    requiredField: 'هذا الحقل مطلوب',
  },
  en: {
    // Brand
    brandName: 'Hudoor',
    brandTagline: 'Smart Attendance SaaS for Private Tutors & Education Centers',

    // Nav
    dashboard: 'Dashboard',
    liveSession: 'Live QR Session',
    fastRoster: 'Fast-Tap Roster',
    students: 'Students',
    groups: 'Groups & Subjects',
    reports: 'Reports & Analytics',
    rollover: 'Year Rollover',
    logout: 'Log Out',
    changePassword: 'Change Password',
    welcome: 'Welcome',

    // Login
    loginTitle: 'Sign in to Hudoor Platform',
    teacherTab: 'Teacher Account',
    studentTab: 'Student Account',
    adminTab: 'Platform Admin',
    usernameLabel: 'Username',
    studentCodeLabel: 'Student Code (HD-XXXXXX)',
    passwordLabel: 'Password',
    loginBtn: 'Sign In',
    loggingIn: 'Verifying...',

    // Dashboard
    totalStudents: 'Total Students',
    activeGroups: 'Active Groups',
    totalSessions: 'Conducted Sessions',
    attendanceRate: 'Overall Attendance',
    atRiskStudents: 'At-Risk Students',
    startLiveSessionBtn: 'Start Live QR Session',
    addStudentBtn: 'Register New Student',

    // Attendance
    present: 'Present',
    late: 'Late',
    excused: 'Excused',
    absent: 'Absent',
    unrecorded: 'Unrecorded',
    markAllPresent: 'Mark All Present',
    closeSessionBtn: 'Close Session & Reconcile Absences',
    sessionClosedMsg: 'Session closed and absent students reconciled',
    scanQrNotice: 'Point your camera at the screen QR code to check in instantly',
    tokenRotatingIn: 'Rotating in',
    seconds: 's',

    // Student Portal
    studentPortalTitle: 'Student Attendance Portal',
    myAttendance: 'My Attendance',
    scanQrBtn: 'Scan QR Attendance Code',
    mySummary: 'My Summary',
    myPercentage: 'My Attendance Rate',

    // Actions & Common
    save: 'Save',
    cancel: 'Cancel',
    search: 'Search by name or code...',
    exportCsv: 'Export Excel / CSV',
    actionSuccess: 'Action completed successfully',
    actionError: 'An error occurred, please try again',
    requiredField: 'This field is required',
  },
};

interface LanguageContextType {
  language: Language;
  direction: 'rtl' | 'ltr';
  setLanguage: (lang: Language) => void;
  toggleLanguage: () => void;
  t: (key: string) => string;
}

const LanguageContext = createContext<LanguageContextType | undefined>(undefined);

export const LanguageProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [language, setLanguage] = useState<Language>(() => {
    const saved = localStorage.getItem('hudoor_lang');
    return saved === 'en' ? 'en' : 'ar';
  });

  const direction = language === 'ar' ? 'rtl' : 'ltr';

  useEffect(() => {
    document.documentElement.setAttribute('lang', language);
    document.documentElement.setAttribute('dir', direction);
    localStorage.setItem('hudoor_lang', language);
  }, [language, direction]);

  const toggleLanguage = () => {
    setLanguage(prev => (prev === 'ar' ? 'en' : 'ar'));
  };

  const t = (key: string): string => {
    return translations[language][key] || key;
  };

  return (
    <LanguageContext.Provider value={{ language, direction, setLanguage, toggleLanguage, t }}>
      {children}
    </LanguageContext.Provider>
  );
};

export const useLanguage = () => {
  const context = useContext(LanguageContext);
  if (!context) {
    throw new Error('useLanguage must be used within a LanguageProvider');
  }
  return context;
};
